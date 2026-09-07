using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.Items;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using UnityEngine;

namespace MSC.Save.Integration
{
    /// <summary>One identity and physical owner across the existing item and vehicle save domains.</summary>
    internal sealed class VehicleItemSaveRestorePlanFactory : ISaveRestorePlanFactory
    {
        private readonly ItemWorldRuntime items;
        private readonly VehicleSaveParticipant vehicles;
        private readonly WorldEntitySaveParticipant world;
        private readonly DeferredStableEntityStore deferred;
        private readonly Action<ItemDomainSaveDto> normalizeItems;
        private readonly Dictionary<string, Func<string, PartDefinition>> definitions =
            new Dictionary<string, Func<string, PartDefinition>>(StringComparer.Ordinal);
        private Dictionary<string, string> ownerByItem = new Dictionary<string, string>(StringComparer.Ordinal);
        private bool rollingBack;

        internal VehicleItemSaveRestorePlanFactory(ItemWorldRuntime items, VehicleSaveParticipant vehicles,
            WorldEntitySaveParticipant world, DeferredStableEntityStore deferred, Action<ItemDomainSaveDto> normalizeItems = null)
        {
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            this.vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.deferred = deferred ?? throw new ArgumentNullException(nameof(deferred));
            this.normalizeItems = normalizeItems;
            vehicles.BindingRegistered += HandleBindingRegistered;
            vehicles.BeforeBindingRestore = BeforeBindingRestore;
        }

        internal bool IsOwnerUnavailable(string itemId)
        {
            if (ownerByItem.TryGetValue(itemId, out string owner)) return !vehicles.TryResolve(owner, out _);
            foreach (VehiclePersistenceBinding binding in vehicles.LoadedBindings)
                if (binding.AssemblyController != null &&
                    binding.AssemblyController.CaptureDynamicPartRegistrations().Any(value => value.Part.StableId.Value == itemId))
                    return false;
            // A newly purchased item can reach aggregate unload before the next save/load builds an ownership plan.
            foreach (DeferredStableEntityPayload payload in deferred.Snapshot())
            {
                if (payload.OwnerDomainId != VehicleSaveParticipant.DomainId) continue;
                VehicleSaveRecordDto record = SaveParticipantJson.Deserialize<VehicleSaveRecordDto>(payload.PayloadJson);
                if ((record.assembly?.dynamicParts ?? Array.Empty<DynamicAssemblyPartSaveDto>())
                    .Any(value => value?.part?.stableEntityId == itemId)) return true;
            }
            // An externally owned identity must not independently materialize from the purchase cell.
            return true;
        }

        private void RememberBinding(VehiclePersistenceBinding binding)
        {
            VehicleItemAssemblyBridge bridge = binding.GetComponent<VehicleItemAssemblyBridge>();
            if (bridge == null || bridge.Catalog == null) return;
            // Retain the project definition asset even when an aggregate's scene unloads.
            var catalog = bridge.Catalog;
            definitions[binding.StableVehicleId] = itemId =>
                    catalog.TryResolve(itemId, out PartDefinition definition) ? definition : null;
        }

        private void HandleBindingRegistered(VehiclePersistenceBinding binding)
        {
            RememberBinding(binding);
            if (binding.AssemblyController == null) return;
            foreach (DynamicPartRegistration registration in binding.AssemblyController.CaptureDynamicPartRegistrations())
                world.TransferToExternalOwnership(registration.Part.StableId.Value);
        }

        public SaveRestorePlan Prepare(SaveDocument document, SaveRestorePreparationContext context)
        {
            foreach (VehiclePersistenceBinding binding in vehicles.LoadedBindings) RememberBinding(binding);
            SaveDomainEnvelope itemEnvelope = RequireEnvelope(document, ItemSaveParticipant.DomainId);
            SaveDomainEnvelope vehicleEnvelope = RequireEnvelope(document, VehicleSaveParticipant.DomainId);
            SaveDomainEnvelope worldEnvelope = RequireEnvelope(document, WorldEntitySaveParticipant.DomainId);
            ItemDomainSaveDto itemState = SaveParticipantJson.Deserialize<ItemDomainSaveDto>(itemEnvelope.PayloadJson);
            WorldEntityDomainSaveDto worldState = SaveParticipantJson.Deserialize<WorldEntityDomainSaveDto>(worldEnvelope.PayloadJson);
            VehicleDomainSaveDto vehicleState = SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(vehicleEnvelope.PayloadJson);
            normalizeItems?.Invoke(itemState);
            if (!itemState.TryValidate(items.Definitions, out string failure) ||
                !worldState.TryValidate(out failure) || !vehicleState.TryValidateBasic(out failure))
                throw new InvalidDataException("Item/vehicle ownership preflight failed: " + failure);

            Dictionary<string, string> ownership = NormalizeAndValidate(itemState, worldState, vehicleState,
                (owner, itemId) => definitions.TryGetValue(owner, out Func<string, PartDefinition> resolve)
                    ? resolve(itemId) : null);
            itemEnvelope.PayloadJson = SaveParticipantJson.Serialize(itemState);
            worldEnvelope.PayloadJson = SaveParticipantJson.Serialize(worldState);
            vehicleEnvelope.PayloadJson = SaveParticipantJson.Serialize(vehicleState);
            return new SaveRestorePlan(document, new OwnershipTransaction(this, ownership, context.DeferredEntities));
        }

        // Inputs are freshly parsed private DTOs. No live graph, caller document or storage is changed.
        internal static Dictionary<string, string> NormalizeAndValidate(ItemDomainSaveDto items,
            WorldEntityDomainSaveDto world, VehicleDomainSaveDto vehicles,
            Func<string, string, PartDefinition> resolveDefinition)
        {
            var itemById = items.instances.ToDictionary(value => value.state.stableEntityId, StringComparer.Ordinal);
            var worldById = world.entities.ToDictionary(value => value.stableEntityId, StringComparer.Ordinal);
            var baseIds = new HashSet<string>(StringComparer.Ordinal);
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (VehicleSaveRecordDto vehicle in vehicles.vehicles)
                foreach (PartSaveDto part in vehicle.assembly.parts)
                    if (part == null || !baseIds.Add(part.stableEntityId))
                        throw new InvalidDataException("Duplicate or missing base part across vehicle aggregates.");

            foreach (VehicleSaveRecordDto vehicle in vehicles.vehicles)
            {
                DynamicAssemblyPartSaveDto[] descriptors = vehicle.assembly.dynamicParts ??
                    Array.Empty<DynamicAssemblyPartSaveDto>();
                if (vehicle.assembly.schemaVersion >= VehicleAssemblySaveData.CurrentSchemaVersion &&
                    vehicle.assembly.dynamicParts == null)
                    throw new InvalidDataException("Current assembly lost its dynamic part collection.");
                if (descriptors.Length > 4096) throw new InvalidDataException("Dynamic part collection is oversized.");
                foreach (DynamicAssemblyPartSaveDto descriptor in descriptors)
                {
                    PartSaveDto part = descriptor?.part;
                    string id = part?.stableEntityId;
                    if (!StableEntityId.TryParse(id, out _) || baseIds.Contains(id) ||
                        !owners.TryAdd(id, vehicle.stableVehicleId) || !itemById.TryGetValue(id, out ItemRuntimeSaveRecord item) ||
                        item.state.isConsumed || item.isCanonicalPlacement ||
                        !string.Equals(item.state.definitionId, descriptor.itemDefinitionId, StringComparison.Ordinal))
                        throw new InvalidDataException("Dynamic part identity is missing, duplicated, consumed or mismatched.");
                    PartDefinition definition = resolveDefinition(vehicle.stableVehicleId, descriptor.itemDefinitionId);
                    if (definition == null || definition.DefinitionId != part.partDefinitionId ||
                        part.lifecycleState != PartLifecycleState.Loose && part.lifecycleState != PartLifecycleState.Installed)
                        throw new InvalidDataException("Dynamic part has no reviewed definition or restorable owner.");
                    if (part.hasSteeringAlignment || part.hasCamshaftTiming || part.hasEngineDocking ||
                        part.hasMechanicalCondition || part.hasValveAdjustment || part.hasServiceCaps ||
                        part.hasEngineAdjustment && (descriptor.itemDefinitionId != "item.oil-filter" ||
                            part.engineAdjustment == null || !part.engineAdjustment.IsValidFor(SatsumaEngineAdjustmentKind.OilFilter) ||
                            part.lifecycleState != PartLifecycleState.Installed && part.engineAdjustment.value != 0f))
                        throw new InvalidDataException("Dynamic item has incompatible optional assembly state.");
                    var physical = new WorldEntityStateDto
                    {
                        stableEntityId = id, worldPosition = part.worldPosition, worldRotation = part.worldRotation,
                        linearVelocity = descriptor.linearVelocity, angularVelocity = descriptor.angularVelocity,
                    };
                    if (!physical.TryValidate(out string failure)) throw new InvalidDataException(failure);
                    MountSaveDto[] occupied = vehicle.assembly.mounts.Where(value =>
                        value != null && value.installedPartStableEntityId == id).ToArray();
                    if (part.lifecycleState == PartLifecycleState.Installed
                        ? occupied.Length != 1 || occupied[0].mountId != part.installedMountId
                        : occupied.Length != 0 || !string.IsNullOrEmpty(part.installedMountId))
                        throw new InvalidDataException("Dynamic part and mount occupancy disagree.");
                    if (worldById.ContainsKey(id))
                        throw new InvalidDataException("Dynamic part has a second world-entity physical owner.");
                }
            }

            foreach (ItemRuntimeSaveRecord item in items.instances)
            {
                string id = item.state.stableEntityId;
                if (item.state.isConsumed || owners.ContainsKey(id)) continue;
                var candidates = vehicles.vehicles.Where(value =>
                    resolveDefinition(value.stableVehicleId, item.state.definitionId) != null).ToArray();
                if (candidates.Length == 0)
                {
                    if (!string.IsNullOrEmpty(VehicleItemPartCatalog.ExpectedPartDefinitionId(item.state.definitionId)))
                        throw new InvalidDataException("Purchased assembly item has no known aggregate catalog: " + id);
                    continue;
                }
                if (candidates.Length != 1 || item.isCanonicalPlacement || baseIds.Contains(id))
                    throw new InvalidDataException("Purchased assembly item has ambiguous ownership or a base identity collision.");
                VehicleSaveRecordDto owner = candidates[0];
                if (owner.assembly.schemaVersion >= VehicleAssemblySaveData.CurrentSchemaVersion)
                    throw new InvalidDataException("Current item save is missing its dynamic assembly descriptor.");
                if (!worldById.TryGetValue(id, out WorldEntityStateDto physical))
                    throw new InvalidDataException("Legacy purchased assembly item is missing its authoritative world pose.");
                PartDefinition definition = resolveDefinition(owner.stableVehicleId, item.state.definitionId);
                var descriptor = new DynamicAssemblyPartSaveDto
                {
                    itemDefinitionId = item.state.definitionId,
                    part = new PartSaveDto
                    {
                        stableEntityId = id, partDefinitionId = definition.DefinitionId,
                        lifecycleState = PartLifecycleState.Loose, installedMountId = string.Empty,
                        worldPosition = physical.worldPosition, worldRotation = physical.worldRotation,
                    },
                    linearVelocity = physical.linearVelocity, angularVelocity = physical.angularVelocity,
                    sleeping = physical.sleeping,
                };
                // Preserve the old schema's latch semantics until the controller performs its exact graph migration.
                owner.assembly.dynamicParts = (owner.assembly.dynamicParts ?? Array.Empty<DynamicAssemblyPartSaveDto>())
                    .Concat(new[] { descriptor }).ToArray();
                owners.Add(id, owner.stableVehicleId);
                worldById.Remove(id);
                item.materializationPosition = physical.worldPosition;
                item.materializationRotation = physical.worldRotation;
            }
            world.entities = worldById.Values.OrderBy(value => value.stableEntityId, StringComparer.Ordinal).ToArray();
            return owners;
        }

        private void BeforeBindingRestore(VehiclePersistenceBinding binding, VehicleSaveRecordDto record)
        {
            if (rollingBack) return;
            DynamicAssemblyPartSaveDto[] descriptors = record.assembly.dynamicParts ?? Array.Empty<DynamicAssemblyPartSaveDto>();
            if (descriptors.Length == 0) return;
            VehicleItemAssemblyBridge bridge = binding.GetComponent<VehicleItemAssemblyBridge>();
            if (bridge == null) throw new InvalidDataException("Dynamic assembly has no configured item bridge.");
            foreach (DynamicAssemblyPartSaveDto descriptor in descriptors)
            {
                string id = descriptor.part.stableEntityId;
                if (items.TryGetInstance(id, out _)) continue;
                if (!deferred.TryPeek(ItemSaveParticipant.DomainId, id, out DeferredStableEntityPayload pending))
                    throw new InvalidDataException("Dynamic assembly item was not materialized or deferred with its owner: " + id);
                ItemRuntimeSaveRecord item = SaveParticipantJson.Deserialize<ItemRuntimeSaveRecord>(pending.PayloadJson);
                if (item.state.stableEntityId != id || item.state.definitionId != descriptor.itemDefinitionId ||
                    !item.TryValidate(items.Definitions, out string failure))
                    throw new InvalidDataException("Deferred assembly item is incompatible: " + id);
                if (items.MaterializeForRestore(item) == null)
                    throw new InvalidDataException("Could not materialize deferred assembly item: " + id);
                deferred.TryTake(ItemSaveParticipant.DomainId, id, out _);
            }
            bridge.ReconcileBindings(descriptors.Select(value => value.part.stableEntityId).ToArray());
        }

        private static SaveDomainEnvelope RequireEnvelope(SaveDocument document, string domainId) =>
            document.Domains.SingleOrDefault(value => value.DomainId == domainId) ??
            throw new InvalidDataException("Assembly item restore requires sibling domain: " + domainId);

        private sealed class OwnershipTransaction : ISaveRestoreTransaction, ISaveRestoreCheckpointBoundary
        {
            private readonly VehicleItemSaveRestorePlanFactory owner;
            private readonly Dictionary<string, string> newOwners;
            private readonly DeferredStableEntityStore staged;
            private Dictionary<string, string> oldOwners;
            private DeferredStableEntityPayload[] oldDeferred;
            private ItemDynamicRestoreTransaction itemTransaction;
            private object worldCheckpoint;
            private readonly List<GraphCheckpoint> graphs = new List<GraphCheckpoint>();
            private bool begun;

            internal OwnershipTransaction(VehicleItemSaveRestorePlanFactory owner,
                Dictionary<string, string> newOwners, DeferredStableEntityStore staged)
            { this.owner = owner; this.newOwners = newOwners; this.staged = staged; }

            public void BeforeCaptureCheckpoints()
            {
                foreach (VehiclePersistenceBinding binding in owner.vehicles.LoadedBindings)
                    binding.AssemblyController?.CancelPendingInstallTransitionsForRestore();
            }

            public void Begin()
            {
                BeforeCaptureCheckpoints();
                oldOwners = owner.ownerByItem;
                oldDeferred = owner.deferred.Snapshot();
                worldCheckpoint = owner.world.CaptureCheckpoint();
                foreach (VehiclePersistenceBinding binding in owner.vehicles.LoadedBindings)
                {
                    VehicleAssemblyController assembly = binding.AssemblyController;
                    if (assembly == null) continue;
                    graphs.Add(new GraphCheckpoint(assembly, assembly.CaptureDynamicPartRegistrations(), assembly.CaptureSaveData()));
                }
                itemTransaction = owner.items.BeginDynamicRestoreTransaction();
                begun = true;
                owner.ownerByItem = newOwners;
                foreach (GraphCheckpoint graph in graphs) SetMembership(graph.Assembly, Array.Empty<DynamicPartRegistration>());
                owner.items.SetExternalOwnershipPlan(newOwners.Keys.ToArray());
                foreach (string id in newOwners.Keys) owner.world.TransferToExternalOwnership(id, staged);
            }

            public void BeforeRollback()
            {
                if (!begun) return;
                owner.rollingBack = true;
                foreach (GraphCheckpoint graph in graphs) SetMembership(graph.Assembly, Array.Empty<DynamicPartRegistration>());
                itemTransaction.RestoreRetainedInstances();
                owner.ownerByItem = oldOwners;
                foreach (GraphCheckpoint graph in graphs)
                {
                    SetMembership(graph.Assembly, graph.Registrations);
                    graph.Assembly.GetComponent<VehicleItemAssemblyBridge>()?.ReconcileBindings(
                        graph.Registrations.Select(value => value.Part.StableId.Value).ToArray());
                    AssemblyOperationResult result = graph.Assembly.RestoreSaveData(graph.State);
                    if (!result.Succeeded) throw new InvalidOperationException("Assembly ownership rollback failed: " + result.Message);
                }
                // Begin can alter world registrations even when item Apply failed before world was attempted.
                owner.world.Rollback(worldCheckpoint);
            }

            public void Rollback()
            {
                if (!begun) return;
                try
                {
                    itemTransaction.Rollback();
                    owner.ownerByItem = oldOwners;
                    owner.deferred.Restore(oldDeferred);
                }
                finally { owner.rollingBack = false; }
            }

            public void Commit()
            {
                itemTransaction?.Commit();
                owner.rollingBack = false;
            }

            private static void SetMembership(VehicleAssemblyController assembly, DynamicPartRegistration[] registrations)
            {
                if (assembly != null && !assembly.TrySetDynamicPartRegistrationsForRestore(registrations, out string failure))
                    throw new InvalidOperationException("Cannot restore dynamic assembly membership: " + failure);
            }

            private readonly struct GraphCheckpoint
            {
                internal GraphCheckpoint(VehicleAssemblyController assembly, DynamicPartRegistration[] registrations,
                    VehicleAssemblySaveData state) { Assembly = assembly; Registrations = registrations; State = state; }
                internal VehicleAssemblyController Assembly { get; }
                internal DynamicPartRegistration[] Registrations { get; }
                internal VehicleAssemblySaveData State { get; }
            }
        }
    }
}
