using UnityEngine;

namespace MSC.World.Vegetation
{
    /// <summary>
    /// Marks geometry that prevents vegetation painting or placement. Bridge
    /// decks may explicitly allow ground sufficiently far below the deck.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VegetationBlocker : MonoBehaviour
    {
        [SerializeField] private VegetationBlockerKind kind =
            VegetationBlockerKind.Other;
        [SerializeField, Min(0f)] private float groundRoadMaximumGap = 1.25f;
        [SerializeField] private bool bridgeDeckAllowsVegetationBelow = true;
        [SerializeField, Min(0f)] private float bridgeDeckMinimumClearance = 4f;

        public VegetationBlockerKind Kind => kind;
        public Collider BlockerCollider => GetComponent<Collider>();
        public bool IsWater => kind == VegetationBlockerKind.Water;

        public bool BlocksSurface(float blockerHitY, float surfaceHitY)
        {
            float verticalGap = blockerHitY - surfaceHitY;
            switch (kind)
            {
                case VegetationBlockerKind.GroundRoad:
                    return verticalGap >= -0.1f &&
                           verticalGap <= groundRoadMaximumGap;
                case VegetationBlockerKind.BridgeDeck:
                    return !bridgeDeckAllowsVegetationBelow ||
                           verticalGap < bridgeDeckMinimumClearance;
                case VegetationBlockerKind.BridgePillar:
                case VegetationBlockerKind.Building:
                case VegetationBlockerKind.Water:
                case VegetationBlockerKind.Foundation:
                case VegetationBlockerKind.Other:
                default:
                    return true;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            VegetationBlockerKind configuredKind,
            float roadMaximumGap = 1.25f,
            bool allowBelowBridgeDeck = true,
            float bridgeMinimumClearance = 4f)
        {
            kind = configuredKind;
            groundRoadMaximumGap = Mathf.Max(0f, roadMaximumGap);
            bridgeDeckAllowsVegetationBelow = allowBelowBridgeDeck;
            bridgeDeckMinimumClearance = Mathf.Max(0f, bridgeMinimumClearance);
        }
#endif
    }
}
