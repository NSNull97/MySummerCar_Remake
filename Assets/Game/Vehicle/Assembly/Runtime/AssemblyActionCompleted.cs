using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Semantic assembly mutation emitted only after the authoritative graph
    /// has changed. Presentation systems may react to this notification, but
    /// must never use it to drive assembly state.
    /// </summary>
    public enum AssemblyActionKind
    {
        PartInstalled = 0,
        PartRemoved = 1,
        FastenerTightened = 2,
        FastenerLoosened = 3,
        FastenerInserted = 4,
        FastenerRemoved = 5,
        PartBrokenLoose = 6,
    }

    public readonly struct AssemblyActionCompleted
    {
        public AssemblyActionCompleted(
            AssemblyActionKind action,
            PartInstance part,
            string mountId,
            string fastenerDefinitionId,
            Vector3 worldPosition,
            float transferredMassKilograms = 0f,
            Vector3 sourceLinearVelocity = default,
            Vector3 sourceAngularVelocity = default)
        {
            Action = action;
            Part = part;
            MountId = mountId ?? string.Empty;
            FastenerDefinitionId = fastenerDefinitionId ?? string.Empty;
            WorldPosition = worldPosition;
            TransferredMassKilograms = Mathf.Max(
                0f,
                transferredMassKilograms);
            SourceLinearVelocity = sourceLinearVelocity;
            SourceAngularVelocity = sourceAngularVelocity;
        }

        public AssemblyActionKind Action { get; }
        public PartInstance Part { get; }
        public string MountId { get; }
        public string FastenerDefinitionId { get; }
        public Vector3 WorldPosition { get; }
        public float TransferredMassKilograms { get; }
        public Vector3 SourceLinearVelocity { get; }
        public Vector3 SourceAngularVelocity { get; }
    }
}
