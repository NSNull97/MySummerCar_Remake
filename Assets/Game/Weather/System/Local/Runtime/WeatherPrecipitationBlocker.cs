using System;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    /// <summary>
    /// Explicit authoring marker for roofs and rain-blocking geometry. Physics
    /// remains handled by ordinary colliders and the bounded roof resolver.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeatherPrecipitationBlocker : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private MonoBehaviour owningZoneComponent;
        [SerializeField] private Collider[] blockerColliders =
            Array.Empty<Collider>();

        public string StableId => stableId;
        public IInteriorZone OwningZone => owningZoneComponent as IInteriorZone;
        public MonoBehaviour OwningZoneComponent => owningZoneComponent;
        public Collider[] BlockerColliders => blockerColliders;
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(stableId) &&
            OwningZone != null &&
            blockerColliders != null && blockerColliders.Length > 0;

        public void ConfigureForAuthoring(
            string authoredStableId,
            MonoBehaviour authoredOwningZone,
            params Collider[] authoredColliders)
        {
            stableId = authoredStableId?.Trim() ?? string.Empty;
            owningZoneComponent = authoredOwningZone;
            blockerColliders = authoredColliders ?? Array.Empty<Collider>();
        }
    }
}
