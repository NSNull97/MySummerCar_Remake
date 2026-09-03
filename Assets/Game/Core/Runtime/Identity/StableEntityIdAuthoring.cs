using UnityEngine;

namespace MSC.Core.Identity
{
    /// <summary>
    /// Stores a persistent ID authored by explicit Editor commands. IDs are never silently regenerated at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StableEntityIdAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Canonical 32-character lower-case GUID. Change only through the stable ID Editor tools.")]
        private string stableId = string.Empty;

        public string SerializedId => stableId;

        public bool TryGetStableId(out StableEntityId entityId)
        {
            return StableEntityId.TryParse(stableId, out entityId);
        }

        /// <summary>
        /// Assigns an identity supplied by a project-owned runtime factory.
        /// This method never generates an ID and refuses to replace a different
        /// authored identity, so save materialization cannot silently mutate
        /// canonical scene identities.
        /// </summary>
        public void InitializeExplicitRuntimeId(StableEntityId entityId)
        {
            if (!entityId.IsValid)
            {
                throw new System.ArgumentException(
                    "Runtime stable identity must be valid.",
                    nameof(entityId));
            }

            if (TryGetStableId(out StableEntityId existing) &&
                existing != entityId)
            {
                throw new System.InvalidOperationException(
                    $"Stable identity '{existing}' cannot be replaced by " +
                    $"'{entityId}'.");
            }

            stableId = entityId.Value;
        }
    }
}
