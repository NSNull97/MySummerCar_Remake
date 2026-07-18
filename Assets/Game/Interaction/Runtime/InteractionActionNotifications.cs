using System;
using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Interaction.Notifications
{
    /// <summary>
    /// Completed, player-intended carry actions. Recovery releases such as a
    /// lost or destroyed target deliberately do not appear in this contract.
    /// </summary>
    public enum InteractionActionKind
    {
        Pickup = 0,
        Drop = 1,
        Place = 2,
        Throw = 3,
        MountHandoff = 4,
    }

    /// <summary>
    /// Vendor-neutral notification emitted only after an interaction action has
    /// successfully changed gameplay state.
    /// </summary>
    public readonly struct InteractionActionCompleted
    {
        public InteractionActionCompleted(
            InteractionActionKind action,
            StableEntityId targetStableId,
            Vector3 worldPosition)
        {
            if (!Enum.IsDefined(typeof(InteractionActionKind), action))
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            Action = action;
            TargetStableId = targetStableId;
            WorldPosition = new Vector3(
                FiniteOrZero(worldPosition.x),
                FiniteOrZero(worldPosition.y),
                FiniteOrZero(worldPosition.z));
        }

        public InteractionActionKind Action { get; }

        public StableEntityId TargetStableId { get; }

        public Vector3 WorldPosition { get; }

        private static float FiniteOrZero(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }

    /// <summary>
    /// Narrow observation boundary for presentation integrations. Implementers
    /// remain authoritative for deciding whether an action succeeded.
    /// </summary>
    public interface IInteractionActionSource
    {
        event Action<InteractionActionCompleted> ActionCompleted;
    }
}
