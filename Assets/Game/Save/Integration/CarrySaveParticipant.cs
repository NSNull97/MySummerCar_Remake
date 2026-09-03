using System;
using System.IO;
using System.Linq;
using MSC.Interaction;
using MSC.Interaction.Carrying;
using UnityEngine;

namespace MSC.Save.Integration
{
    internal sealed class CarrySaveParticipant : ISaveParticipant
    {
        public const string DomainId = "interaction.carry";

        private readonly GameObject player;
        private readonly Transform viewpoint;
        private readonly PhysicalCarryController carry;
        private readonly WorldEntitySaveParticipant worldEntities;
        private readonly DeferredStableEntityStore deferredEntities;

        public CarrySaveParticipant(
            GameObject player,
            WorldEntitySaveParticipant worldEntities,
            DeferredStableEntityStore deferredEntities)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.worldEntities = worldEntities ??
                throw new ArgumentNullException(nameof(worldEntities));
            this.deferredEntities = deferredEntities ??
                throw new ArgumentNullException(nameof(deferredEntities));
            carry = player.GetComponentInChildren<PhysicalCarryController>(true) ??
                throw new InvalidOperationException(
                    "Production player has no PhysicalCarryController save boundary.");
            Camera camera = player.GetComponentInChildren<Camera>(true);
            viewpoint = camera != null ? camera.transform : player.transform;
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                CarriedObjectSaveState.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.CarryState,
                WorldEntitySaveParticipant.DomainId,
                PlayerSaveParticipant.DomainId);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(CaptureStateIncludingDeferred());

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            CarriedObjectSaveState state =
                SaveParticipantJson.Deserialize<CarriedObjectSaveState>(
                    envelope.PayloadJson);
            if (!state.TryValidate(out string failure))
            {
                throw new InvalidDataException(
                    "Carry save preflight failed: " + failure);
            }

            return state;
        }

        public object CaptureCheckpoint() => carry.CaptureSaveState();

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            CarriedObjectSaveState state =
                (CarriedObjectSaveState)preparedState;
            RemoveDeferredOwnerState(context.DeferredEntities);
            if (!state.HasCarriedObject)
            {
                RestoreNow(state, null);
                return;
            }

            if (worldEntities.TryResolve(
                    state.StableEntityId,
                    out PhysicsPickupTarget target))
            {
                RestoreNow(state, target);
                return;
            }

            context.DeferredEntities.Enqueue(ToDeferred(state));
            context.UnresolvedContent.Add(
                DomainId,
                state.StableEntityId,
                UnresolvedContentReason.DeferredUntilCellLoad,
                "Carry attachment is retained until the carried stable entity loads.");
        }

        public void Rollback(object checkpoint)
        {
            CarriedObjectSaveState state = (CarriedObjectSaveState)checkpoint;
            PhysicsPickupTarget target = null;
            if (state.HasCarriedObject &&
                !worldEntities.TryResolve(state.StableEntityId, out target))
            {
                throw new InvalidOperationException(
                    "Carry rollback target is no longer loaded.");
            }

            RestoreNow(state, target);
        }

        public void ApplyDeferredAfterSceneLoad()
        {
            DeferredStableEntityPayload[] candidates = deferredEntities.Snapshot()
                .Where(payload => string.Equals(
                    payload.OwnerDomainId,
                    DomainId,
                    StringComparison.Ordinal))
                .ToArray();
            if (candidates.Length > 1)
            {
                throw new InvalidDataException(
                    "More than one deferred carry attachment exists for one player.");
            }

            if (candidates.Length == 0)
            {
                return;
            }

            DeferredStableEntityPayload payload = candidates[0];
            if (!worldEntities.TryResolve(
                    payload.StableEntityId,
                    out PhysicsPickupTarget target))
            {
                return;
            }

            CarriedObjectSaveState state =
                SaveParticipantJson.Deserialize<CarriedObjectSaveState>(
                    payload.PayloadJson);
            if (!state.TryValidate(out string failure) ||
                !string.Equals(
                    state.StableEntityId,
                    payload.StableEntityId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Deferred carry attachment is invalid: " + failure);
            }

            RestoreNow(state, target);
            deferredEntities.TryTake(
                DomainId,
                payload.StableEntityId,
                out _);
        }

        private CarriedObjectSaveState CaptureStateIncludingDeferred()
        {
            CarriedObjectSaveState current = carry.CaptureSaveState();
            if (current.HasCarriedObject)
            {
                return current;
            }

            DeferredStableEntityPayload[] candidates = deferredEntities.Snapshot()
                .Where(payload => string.Equals(
                    payload.OwnerDomainId,
                    DomainId,
                    StringComparison.Ordinal))
                .ToArray();
            if (candidates.Length == 0)
            {
                return current;
            }

            if (candidates.Length > 1)
            {
                throw new InvalidDataException(
                    "More than one deferred carry attachment exists for one player.");
            }

            CarriedObjectSaveState pending =
                SaveParticipantJson.Deserialize<CarriedObjectSaveState>(
                    candidates[0].PayloadJson);
            if (!pending.TryValidate(out string failure))
            {
                throw new InvalidDataException(
                    "Deferred carry attachment is invalid: " + failure);
            }

            return pending;
        }

        private void RestoreNow(
            CarriedObjectSaveState state,
            PhysicsPickupTarget target)
        {
            Vector3 origin = viewpoint != null
                ? viewpoint.position
                : player.transform.position;
            Vector3 direction = viewpoint != null
                ? viewpoint.forward
                : player.transform.forward;
            var interactionContext = new InteractionContext(
                player,
                origin,
                direction);
            if (!carry.TryRestoreSaveState(
                    state,
                    target,
                    interactionContext,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Carry restore failed after preflight: " + failure);
            }
        }

        private static DeferredStableEntityPayload ToDeferred(
            CarriedObjectSaveState state) => new DeferredStableEntityPayload
        {
            StableEntityId = state.StableEntityId,
            OwnerDomainId = DomainId,
            SchemaVersion = CarriedObjectSaveState.CurrentSchemaVersion,
            PayloadJson = SaveParticipantJson.Serialize(state),
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
