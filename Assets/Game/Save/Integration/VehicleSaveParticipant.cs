using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Items;
using MSC.Vehicle.ItemsIntegration;
using MSC.World.Streaming;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Save.Integration
{
    internal sealed class VehicleSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "vehicle.satsuma";

        private readonly DeferredStableEntityStore deferredEntities;
        private readonly ProductionWorldStreamingService worldStreaming;
        private readonly ImportantObjectRecoveryFormation recoveryFormation;
        private readonly float recoveryMinimumY;
        private readonly ISatsumaKeyAccess keyAccess;
        private readonly ItemWorldRuntime itemRuntime;
        internal IEnumerable<VehiclePersistenceBinding> LoadedBindings => loadedVehicles.Values.Where(value => value != null);
        internal event Action<VehiclePersistenceBinding> BindingRegistered;
        internal Action<VehiclePersistenceBinding, VehicleSaveRecordDto> BeforeBindingRestore;
        private readonly Dictionary<string, VehiclePersistenceBinding> loadedVehicles =
            new Dictionary<string, VehiclePersistenceBinding>(StringComparer.Ordinal);
        private readonly Dictionary<EntityId, SuspendedVehicleBody>
            startupGuards = new Dictionary<EntityId, SuspendedVehicleBody>();
        private readonly Dictionary<EntityId, SuspendedVehicleBody>
            streamingGuards = new Dictionary<EntityId, SuspendedVehicleBody>();

        public VehicleSaveParticipant(
            DeferredStableEntityStore deferredEntities)
            : this(deferredEntities, null, null, -64f)
        {
        }

        public VehicleSaveParticipant(
            DeferredStableEntityStore deferredEntities,
            ProductionWorldStreamingService worldStreaming,
            ImportantObjectRecoveryFormation recoveryFormation = null,
            float recoveryMinimumY = -64f,
            ISatsumaKeyAccess keyAccess = null)
            : this(deferredEntities, worldStreaming, recoveryFormation, recoveryMinimumY, keyAccess, null)
        {
        }

        public VehicleSaveParticipant(
            DeferredStableEntityStore deferredEntities,
            ProductionWorldStreamingService worldStreaming,
            ImportantObjectRecoveryFormation recoveryFormation,
            float recoveryMinimumY,
            ISatsumaKeyAccess keyAccess,
            ItemWorldRuntime itemRuntime)
        {
            this.deferredEntities = deferredEntities ??
                throw new ArgumentNullException(nameof(deferredEntities));
            this.worldStreaming = worldStreaming;
            this.recoveryFormation = recoveryFormation;
            this.recoveryMinimumY = recoveryMinimumY;
            this.keyAccess = keyAccess;
            this.itemRuntime = itemRuntime;
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                VehicleDomainSaveDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.VehicleAssembly,
                itemRuntime != null
                    ? new[] { ItemSaveParticipant.DomainId, WorldEntitySaveParticipant.DomainId }
                    : new[] { WorldEntitySaveParticipant.DomainId });
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(CaptureDomainState());

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            VehicleDomainSaveDto state =
                SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(
                    envelope.PayloadJson);
            if (!state.TryValidateBasic(out string failure))
            {
                throw new InvalidDataException(
                    "Vehicle save preflight failed: " + failure);
            }

            for (int index = 0; index < state.vehicles.Length; index++)
            {
                VehicleSaveRecordDto record = state.vehicles[index];
                if (TryResolve(record.stableVehicleId, out VehiclePersistenceBinding binding) &&
                    !binding.CanRestore(record, out failure))
                {
                    throw new InvalidDataException(
                        $"Vehicle '{record.stableVehicleId}' failed runtime preflight: {failure}");
                }
            }

            return state;
        }

        public object CaptureCheckpoint() => CaptureDomainState();

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            VehicleDomainSaveDto state = (VehicleDomainSaveDto)preparedState;
            RemoveDeferredOwnerState(context.DeferredEntities);
            for (int index = 0; index < state.vehicles.Length; index++)
            {
                VehicleSaveRecordDto record = state.vehicles[index];
                if (TryResolve(record.stableVehicleId, out VehiclePersistenceBinding binding))
                {
                    BeforeBindingRestore?.Invoke(binding, record);
                    RecoverInvalidVehicleBodiesIfNeeded(
                        binding,
                        record,
                        context.UnresolvedContent);
                    RestoreBinding(binding, record);
                    continue;
                }

                context.DeferredEntities.Enqueue(ToDeferred(record));
                context.UnresolvedContent.Add(
                    DomainId,
                    record.stableVehicleId,
                    UnresolvedContentReason.DeferredUntilCellLoad,
                    "Vehicle aggregate is retained until its owning cell or gameplay layer loads.");
            }
        }

        private void RecoverInvalidVehicleBodiesIfNeeded(
            VehiclePersistenceBinding binding,
            VehicleSaveRecordDto record,
            UnresolvedContentReport unresolvedContent)
        {
            if (recoveryFormation == null || binding == null || record == null)
            {
                return;
            }

            if (record.physics != null &&
                record.physics.worldPosition.y < recoveryMinimumY)
            {
                Rigidbody chassis = binding.Chassis;
                if (chassis == null)
                {
                    throw new InvalidDataException(
                        $"Vehicle '{record.stableVehicleId}' is below the world " +
                        "but has no chassis Rigidbody for safe recovery.");
                }

                Vector3 previousPosition = record.physics.worldPosition;
                if (!recoveryFormation.TryReserve(
                        chassis.gameObject,
                        CaptureBodyPose(chassis).rotation,
                        out Pose recoveredPose,
                        out string failure))
                {
                    throw new InvalidDataException(
                        $"Vehicle '{record.stableVehicleId}' could not be " +
                        "recovered in front of the player home: " + failure);
                }

                record.physics.worldPosition = recoveredPose.position;
                record.physics.worldRotation = recoveredPose.rotation;
                record.physics.linearVelocity = Vector3.zero;
                record.physics.angularVelocity = Vector3.zero;
                record.physics.sleeping = true;
                unresolvedContent?.Add(
                    DomainId,
                    record.stableVehicleId,
                    UnresolvedContentReason.ParticipantReported,
                    $"Recovered vehicle chassis from ({previousPosition.x:F3}, " +
                    $"{previousPosition.y:F3}, {previousPosition.z:F3}) to " +
                    $"'{ImportantObjectRecoveryFormation.AnchorId}' at " +
                    $"({recoveredPose.position.x:F3}, " +
                    $"{recoveredPose.position.y:F3}, " +
                    $"{recoveredPose.position.z:F3}); aggregate stable ID preserved.");
            }

            if (record.assembly?.parts == null)
            {
                return;
            }

            VehicleAssemblyController assembly = binding.AssemblyController;
            if (assembly == null)
            {
                return;
            }

            var runtimeParts = assembly.AllRuntimeParts
                .Where(part => part != null && part.StableId.IsValid)
                .ToDictionary(
                    part => part.StableId.Value,
                    part => part,
                    StringComparer.Ordinal);
            PartSaveDto[] recover = record.assembly.parts.Concat(
                    (record.assembly.dynamicParts ?? Array.Empty<DynamicAssemblyPartSaveDto>()).Select(value => value.part))
                .Where(part =>
                    part != null &&
                    part.lifecycleState == PartLifecycleState.Loose &&
                    part.worldPosition.y < recoveryMinimumY)
                .OrderBy(part => part.stableEntityId, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < recover.Length; index++)
            {
                PartSaveDto partState = recover[index];
                if (!runtimeParts.TryGetValue(
                        partState.stableEntityId,
                        out PartInstance runtimePart) ||
                    runtimePart.Body == null)
                {
                    throw new InvalidDataException(
                        $"Loose vehicle part '{partState.stableEntityId}' is " +
                        "below the world but has no runtime body for safe recovery.");
                }

                Vector3 previousPosition = partState.worldPosition;
                Quaternion authoredRotation = runtimePart.transform.rotation;
                if (!recoveryFormation.TryReserve(
                        runtimePart.gameObject,
                        authoredRotation,
                        out Pose recoveredPose,
                        out string failure))
                {
                    throw new InvalidDataException(
                        $"Loose vehicle part '{partState.stableEntityId}' could " +
                        "not be recovered in front of the player home: " + failure);
                }

                partState.worldPosition = recoveredPose.position;
                partState.worldRotation = recoveredPose.rotation;
                DynamicAssemblyPartSaveDto recoveredDynamic = (record.assembly.dynamicParts ?? Array.Empty<DynamicAssemblyPartSaveDto>())
                    .FirstOrDefault(value => value.part.stableEntityId == partState.stableEntityId);
                if (recoveredDynamic != null)
                {
                    recoveredDynamic.linearVelocity = Vector3.zero;
                    recoveredDynamic.angularVelocity = Vector3.zero;
                    recoveredDynamic.sleeping = true;
                }
                unresolvedContent?.Add(
                    DomainId,
                    partState.stableEntityId,
                    UnresolvedContentReason.ParticipantReported,
                    $"Recovered loose vehicle part '{partState.partDefinitionId}' " +
                    $"from ({previousPosition.x:F3}, {previousPosition.y:F3}, " +
                    $"{previousPosition.z:F3}) to " +
                    $"'{ImportantObjectRecoveryFormation.AnchorId}' at " +
                    $"({recoveredPose.position.x:F3}, " +
                    $"{recoveredPose.position.y:F3}, " +
                    $"{recoveredPose.position.z:F3}); lifecycle and stable ID preserved.");
            }
        }

        public void Rollback(object checkpoint)
        {
            VehicleDomainSaveDto state = (VehicleDomainSaveDto)checkpoint;
            for (int index = 0; index < state.vehicles.Length; index++)
            {
                VehicleSaveRecordDto record = state.vehicles[index];
                if (TryResolve(record.stableVehicleId, out VehiclePersistenceBinding binding))
                {
                    RestoreBinding(binding, record);
                }
            }
        }

        public void RegisterScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                RegisterHierarchy(roots[rootIndex]);
            }
        }

        public void CompleteSceneLoad(Scene scene)
        {
            if (worldStreaming == null || !scene.IsValid() || !scene.isLoaded ||
                !worldStreaming.TryGetCellIdForScene(scene, out string cellId))
            {
                return;
            }

            SuspendedVehicleBody[] ready = streamingGuards.Values
                .Where(candidate =>
                    candidate.Body != null &&
                    string.Equals(
                        candidate.CellId,
                        cellId,
                        StringComparison.Ordinal))
                .OrderBy(candidate => candidate.StableBodyId, StringComparer.Ordinal)
                .ToArray();
            RestoreBodiesSafely(ready);
            for (int index = 0; index < ready.Length; index++)
            {
                streamingGuards.Remove(ready[index].InstanceId);
            }
        }

        public void GuardPersistentVehiclesUntilWorldReady()
        {
            foreach (VehiclePersistenceBinding binding in loadedVehicles.Values
                         .Where(candidate => candidate != null)
                         .OrderBy(
                             candidate => candidate.StableVehicleId,
                             StringComparer.Ordinal))
            {
                CaptureBindingBodies(
                    binding,
                    cellId: string.Empty,
                    startupGuards,
                    includeOnlyUnsupportedCells: false);
            }

            FreezeBodies(startupGuards.Values);
        }

        public void ReleasePersistentVehicleStartupGuard()
        {
            SuspendedVehicleBody[] guarded = startupGuards.Values
                .Where(candidate => candidate.Body != null)
                .OrderBy(candidate => candidate.StableBodyId, StringComparer.Ordinal)
                .ToArray();
            RestoreBodiesSafely(guarded);
            startupGuards.Clear();
        }

        public void RegisterHierarchy(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            VehiclePersistenceBinding[] bindings =
                root.GetComponentsInChildren<VehiclePersistenceBinding>(true);
            for (int bindingIndex = 0;
                 bindingIndex < bindings.Length;
                 bindingIndex++)
            {
                RegisterBinding(bindings[bindingIndex]);
            }
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
            VehiclePersistenceBinding[] owned = loadedVehicles.Values
                .Where(binding => binding != null &&
                                  binding.gameObject.scene.handle == scene.handle)
                .ToArray();
            for (int index = 0; index < owned.Length; index++)
            {
                VehicleSaveRecordDto record = CaptureBinding(owned[index]);
                GuardBindingForSceneTeardown(owned[index]);
                deferredEntities.Enqueue(ToDeferred(record), replaceExisting: true);
                loadedVehicles.Remove(record.stableVehicleId);
            }

            if (!isBaseCellScene)
            {
                return;
            }

            foreach (VehiclePersistenceBinding binding in loadedVehicles.Values
                         .Where(candidate => candidate != null)
                         .OrderBy(
                             candidate => candidate.StableVehicleId,
                             StringComparer.Ordinal))
            {
                CaptureBindingBodies(
                    binding,
                    unloadingCellId,
                    streamingGuards,
                    includeOnlyUnsupportedCells: true);
            }

            FreezeBodies(streamingGuards.Values.Where(candidate =>
                string.Equals(
                    candidate.CellId,
                    unloadingCellId,
                    StringComparison.Ordinal)));
            Physics.SyncTransforms();
        }

        internal bool TryResolve(
            string stableVehicleId,
            out VehiclePersistenceBinding binding)
        {
            if (loadedVehicles.TryGetValue(
                    stableVehicleId ?? string.Empty,
                    out binding) &&
                binding != null)
            {
                return true;
            }

            loadedVehicles.Remove(stableVehicleId ?? string.Empty);
            binding = null;
            return false;
        }

        private VehicleDomainSaveDto CaptureDomainState()
        {
            var byId = new Dictionary<string, VehicleSaveRecordDto>(
                StringComparer.Ordinal);
            DeferredStableEntityPayload[] pending = deferredEntities.Snapshot();
            for (int index = 0; index < pending.Length; index++)
            {
                DeferredStableEntityPayload payload = pending[index];
                if (!string.Equals(payload.OwnerDomainId, DomainId, StringComparison.Ordinal))
                {
                    continue;
                }

                VehicleSaveRecordDto record =
                    SaveParticipantJson.Deserialize<VehicleSaveRecordDto>(
                        payload.PayloadJson);
                if (!record.TryValidateBasic(out string failure) ||
                    !string.Equals(
                        record.stableVehicleId,
                        payload.StableEntityId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Deferred vehicle state is invalid: " + failure);
                }

                byId.Add(record.stableVehicleId, record);
            }

            foreach (KeyValuePair<string, VehiclePersistenceBinding> pair in
                     loadedVehicles.ToArray())
            {
                if (pair.Value == null)
                {
                    loadedVehicles.Remove(pair.Key);
                    continue;
                }

                byId[pair.Key] = CaptureBinding(pair.Value);
            }

            var dto = new VehicleDomainSaveDto
            {
                vehicles = byId.Values
                    .OrderBy(record => record.stableVehicleId, StringComparer.Ordinal)
                    .ToArray(),
            };
            if (!dto.TryValidateBasic(out string failureMessage))
            {
                throw new InvalidDataException(
                    "Captured vehicle domain state is invalid: " + failureMessage);
            }

            return dto;
        }

        private void RegisterBinding(VehiclePersistenceBinding binding)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.StableVehicleId))
            {
                throw new InvalidOperationException(
                    "A loaded VehiclePersistenceBinding has no valid stable aggregate ID.");
            }

            string id = binding.StableVehicleId;
            if (loadedVehicles.TryGetValue(id, out VehiclePersistenceBinding existing) &&
                existing != null && existing != binding)
            {
                throw new InvalidOperationException(
                    $"Duplicate loaded vehicle stable ID '{id}'.");
            }

            if (keyAccess != null)
            {
                binding.BindKeyAccess(keyAccess);
            }

            loadedVehicles[id] = binding;
            if (itemRuntime != null)
            {
                binding.GetComponent<VehicleItemAssemblyBridge>()?.BindRuntime(itemRuntime);
                BindingRegistered?.Invoke(binding);
            }
            if (!deferredEntities.TryPeek(
                    DomainId,
                    id,
                    out DeferredStableEntityPayload pending))
            {
                return;
            }

            VehicleSaveRecordDto record =
                SaveParticipantJson.Deserialize<VehicleSaveRecordDto>(
                    pending.PayloadJson);
            if (!record.TryValidateBasic(out string failure) ||
                !string.Equals(record.stableVehicleId, id, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Deferred state for vehicle '{id}' is invalid: {failure}");
            }

            BeforeBindingRestore?.Invoke(binding, record);
            RecoverInvalidVehicleBodiesIfNeeded(
                binding,
                record,
                unresolvedContent: null);
            RestoreBinding(binding, record);
            deferredEntities.TryTake(DomainId, id, out _);
        }

        private VehicleSaveRecordDto CaptureBinding(
            VehiclePersistenceBinding binding)
        {
            if (binding == null)
            {
                throw new InvalidDataException(
                    "Cannot capture a missing vehicle aggregate binding.");
            }

            if (!binding.TryCapture(
                    out VehicleSaveRecordDto record,
                    out string failure))
            {
                throw new InvalidDataException(
                    "Cannot capture vehicle aggregate: " + failure);
            }

            if (TryGetGuardedBodyState(
                    binding.Chassis,
                    out SuspendedVehicleBody chassisGuard))
            {
                record.physics.worldPosition = chassisGuard.Position;
                record.physics.worldRotation = chassisGuard.Rotation;
                record.physics.linearVelocity = chassisGuard.LinearVelocity;
                record.physics.angularVelocity = chassisGuard.AngularVelocity;
                record.physics.sleeping = chassisGuard.Sleeping;
            }

            foreach (DynamicAssemblyPartSaveDto descriptor in record.assembly.dynamicParts ?? Array.Empty<DynamicAssemblyPartSaveDto>())
            {
                if (binding.AssemblyController.Graph.TryGetPartByStableId(descriptor.part.stableEntityId, out PartInstance part) &&
                    TryGetGuardedBodyState(part.Body, out SuspendedVehicleBody guard))
                {
                    descriptor.part.worldPosition = guard.Position;
                    descriptor.part.worldRotation = guard.Rotation;
                    descriptor.linearVelocity = guard.LinearVelocity;
                    descriptor.angularVelocity = guard.AngularVelocity;
                    descriptor.sleeping = guard.Sleeping;
                }
            }

            return record;
        }

        private void RestoreBinding(
            VehiclePersistenceBinding binding,
            VehicleSaveRecordDto record)
        {
            BeforeBindingRestore?.Invoke(binding, record);
            if (!binding.TryRestore(record, out string failure))
            {
                throw new InvalidOperationException(
                    $"Vehicle '{record.stableVehicleId}' restore failed: {failure}");
            }

            SuspendBodiesWithoutLoadedSupport(binding);
        }

        private void SuspendBodiesWithoutLoadedSupport(
            VehiclePersistenceBinding binding)
        {
            if (worldStreaming == null || binding == null)
            {
                return;
            }

            CaptureBindingBodies(
                binding,
                cellId: string.Empty,
                streamingGuards,
                includeOnlyUnsupportedCells: true);
            FreezeBodies(streamingGuards.Values.Where(candidate =>
                candidate.Body != null &&
                !string.IsNullOrEmpty(candidate.CellId) &&
                !worldStreaming.IsCellLoaded(candidate.CellId)));
            Physics.SyncTransforms();
        }

        private void CaptureBindingBodies(
            VehiclePersistenceBinding binding,
            string cellId,
            IDictionary<EntityId, SuspendedVehicleBody> destination,
            bool includeOnlyUnsupportedCells)
        {
            if (binding == null)
            {
                return;
            }

            TryCaptureBody(
                binding.Chassis,
                binding.StableVehicleId + ":chassis",
                cellId,
                destination,
                includeOnlyUnsupportedCells);

            VehicleAssemblyController assembly = binding.AssemblyController;
            PartInstance[] parts = assembly != null
                ? assembly.AllRuntimeParts
                : Array.Empty<PartInstance>();
            for (int index = 0; index < parts.Length; index++)
            {
                PartInstance part = parts[index];
                if (part == null || part.IsAssemblyRoot || part.Body == null ||
                    part.IsInstalled && !part.UsesDynamicInstalledPhysics)
                {
                    continue;
                }

                TryCaptureBody(
                    part.Body,
                    part.StableId.Value,
                    cellId,
                    destination,
                    includeOnlyUnsupportedCells);
            }
        }

        private static Pose CaptureBodyPose(Rigidbody body)
        {
            // Inactive Unity 6000.6 actors expose zero/identity, not their
            // authored pose. Keep active-body interpolation precision intact.
            return body.gameObject.activeInHierarchy
                ? new Pose(body.position, body.rotation)
                : new Pose(body.transform.position, body.transform.rotation);
        }

        private void TryCaptureBody(
            Rigidbody body,
            string stableBodyId,
            string requestedCellId,
            IDictionary<EntityId, SuspendedVehicleBody> destination,
            bool includeOnlyUnsupportedCells)
        {
            if (body == null || destination.ContainsKey(body.GetEntityId()))
            {
                return;
            }

            Pose bodyPose = CaptureBodyPose(body);
            string bodyCellId = requestedCellId ?? string.Empty;
            if (worldStreaming != null &&
                (!string.IsNullOrEmpty(bodyCellId) ||
                 worldStreaming.TryGetCellIdForPosition(
                     bodyPose.position,
                     out bodyCellId)))
            {
                if (includeOnlyUnsupportedCells &&
                    (string.IsNullOrEmpty(bodyCellId) ||
                     worldStreaming.IsCellLoaded(bodyCellId) &&
                     string.IsNullOrEmpty(requestedCellId)))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(requestedCellId) &&
                    (!worldStreaming.TryGetCellIdForPosition(
                         bodyPose.position,
                         out string currentCellId) ||
                     !string.Equals(
                         currentCellId,
                         requestedCellId,
                         StringComparison.Ordinal)))
                {
                    return;
                }
            }
            else if (includeOnlyUnsupportedCells)
            {
                return;
            }

            var suspended = new SuspendedVehicleBody(
                body,
                stableBodyId,
                bodyCellId);
            destination.Add(suspended.InstanceId, suspended);
        }

        private bool TryGetGuardedBodyState(
            Rigidbody body,
            out SuspendedVehicleBody guarded)
        {
            EntityId instanceId = body != null ? body.GetEntityId() : EntityId.None;
            return startupGuards.TryGetValue(instanceId, out guarded) ||
                   streamingGuards.TryGetValue(instanceId, out guarded);
        }

        private static void GuardBindingForSceneTeardown(
            VehiclePersistenceBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            var bodies = new List<Rigidbody>();
            if (binding.Chassis != null)
            {
                bodies.Add(binding.Chassis);
            }

            VehicleAssemblyController assembly = binding.AssemblyController;
            if (assembly != null)
            {
                PartInstance[] parts = assembly.AllRuntimeParts;
                for (int index = 0; index < parts.Length; index++)
                {
                    PartInstance part = parts[index];
                    if (part != null && !part.IsAssemblyRoot &&
                        part.Body != null &&
                        (!part.IsInstalled || part.UsesDynamicInstalledPhysics))
                    {
                        bodies.Add(part.Body);
                    }
                }
            }

            foreach (Rigidbody body in bodies.Distinct())
            {
                GuardBody(body);
            }

            Physics.SyncTransforms();
        }

        private static void FreezeBodies(
            IEnumerable<SuspendedVehicleBody> bodies)
        {
            foreach (SuspendedVehicleBody suspended in bodies
                         .Where(candidate => candidate?.Body != null)
                         .OrderBy(candidate =>
                             candidate.StableBodyId,
                             StringComparer.Ordinal))
            {
                GuardBody(suspended.Body);
            }
        }

        private static void GuardBody(Rigidbody body)
        {
            body.interpolation = RigidbodyInterpolation.None;
            body.detectCollisions = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
        }

        private static void RestoreBodiesSafely(
            IReadOnlyList<SuspendedVehicleBody> bodies)
        {
            if (bodies == null || bodies.Count == 0)
            {
                return;
            }

            for (int index = 0; index < bodies.Count; index++)
            {
                bodies[index].ApplyPoseWhileGuarded();
            }

            Physics.SyncTransforms();
            for (int index = 0; index < bodies.Count; index++)
            {
                bodies[index].RestoreCollisions();
            }

            Physics.SyncTransforms();
            for (int index = 0; index < bodies.Count; index++)
            {
                bodies[index].ReleaseDynamics();
            }

            Physics.SyncTransforms();
        }

        private sealed class SuspendedVehicleBody
        {
            public SuspendedVehicleBody(
                Rigidbody body,
                string stableBodyId,
                string cellId)
            {
                Body = body;
                StableBodyId = stableBodyId ?? string.Empty;
                CellId = cellId ?? string.Empty;
                InstanceId = body.GetEntityId();
                Pose pose = CaptureBodyPose(body);
                Position = pose.position;
                Rotation = pose.rotation;
                LinearVelocity = body.isKinematic
                    ? Vector3.zero
                    : body.linearVelocity;
                AngularVelocity = body.isKinematic
                    ? Vector3.zero
                    : body.angularVelocity;
                IsKinematic = body.isKinematic;
                UseGravity = body.useGravity;
                Sleeping = !body.isKinematic && body.IsSleeping();
                DetectCollisions = body.detectCollisions;
                Interpolation = body.interpolation;
            }

            public Rigidbody Body { get; }
            public string StableBodyId { get; }
            public string CellId { get; }
            public EntityId InstanceId { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 LinearVelocity { get; }
            public Vector3 AngularVelocity { get; }
            public bool IsKinematic { get; }
            public bool UseGravity { get; }
            public bool Sleeping { get; }
            public bool DetectCollisions { get; }
            public RigidbodyInterpolation Interpolation { get; }

            public void ApplyPoseWhileGuarded()
            {
                if (Body == null)
                {
                    return;
                }

                Body.interpolation = RigidbodyInterpolation.None;
                Body.detectCollisions = false;
                Body.isKinematic = true;
                Body.useGravity = UseGravity;
                Body.position = Position;
                Body.rotation = Rotation;
                Body.transform.SetPositionAndRotation(Position, Rotation);
            }

            public void RestoreCollisions()
            {
                if (Body != null)
                {
                    Body.detectCollisions = DetectCollisions;
                }
            }

            public void ReleaseDynamics()
            {
                if (Body == null)
                {
                    return;
                }

                Body.isKinematic = IsKinematic;
                if (!IsKinematic)
                {
                    Body.linearVelocity = LinearVelocity;
                    Body.angularVelocity = AngularVelocity;
                    if (Sleeping)
                    {
                        Body.Sleep();
                    }
                    else
                    {
                        Body.WakeUp();
                    }
                }

                Body.interpolation = Interpolation;
            }
        }

        private static DeferredStableEntityPayload ToDeferred(
            VehicleSaveRecordDto record) => new DeferredStableEntityPayload
        {
            StableEntityId = record.stableVehicleId,
            OwnerDomainId = DomainId,
            SchemaVersion = VehicleDomainSaveDto.CurrentSchemaVersion,
            PayloadJson = SaveParticipantJson.Serialize(record),
        };

        private static void RemoveDeferredOwnerState(
            DeferredStableEntityStore store)
        {
            DeferredStableEntityPayload[] snapshot = store.Snapshot();
            for (int index = 0; index < snapshot.Length; index++)
            {
                DeferredStableEntityPayload payload = snapshot[index];
                if (string.Equals(payload.OwnerDomainId, DomainId, StringComparison.Ordinal))
                {
                    store.TryTake(DomainId, payload.StableEntityId, out _);
                }
            }
        }
    }
}
