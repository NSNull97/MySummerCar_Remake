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
    }
}
