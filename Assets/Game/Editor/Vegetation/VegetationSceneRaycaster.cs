using System;
using MSC.World.Vegetation;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    internal readonly struct VegetationResolvedHit
    {
        public VegetationResolvedHit(
            RaycastHit configuredHit,
            VegetationSurface configuredSurface)
        {
            Hit = configuredHit;
            Surface = configuredSurface;
        }

        public RaycastHit Hit { get; }
        public VegetationSurface Surface { get; }
    }

    internal static class VegetationSceneRaycaster
    {
        private const int MaximumHits = 256;
        private static readonly RaycastHit[] Hits = new RaycastHit[MaximumHits];
        private static readonly VegetationBlocker[] PendingBlockers =
            new VegetationBlocker[MaximumHits];
        private static readonly RaycastHit[] PendingBlockerHits =
            new RaycastHit[MaximumHits];

        public static bool TryResolve(
            Ray ray,
            float maximumDistance,
            LayerMask relevantLayers,
            VegetationDensityChannel channel,
            out VegetationResolvedHit resolvedHit,
            out VegetationRejectionReason rejectionReason)
        {
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                Hits,
                maximumDistance,
                relevantLayers,
                QueryTriggerInteraction.Ignore);
            if (hitCount == 0)
            {
                resolvedHit = default;
                rejectionReason = VegetationRejectionReason.NoSurface;
                return false;
            }

            SortHitsByDistance(hitCount);
            int pendingBlockerCount = 0;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = Hits[index];
                Collider collider = hit.collider;
                if (collider == null)
                {
                    continue;
                }

                VegetationBlocker blocker =
                    collider.GetComponentInParent<VegetationBlocker>();
                VegetationSurface surface =
                    collider.GetComponentInParent<VegetationSurface>();
                if (blocker == null && surface == null)
                {
                    continue;
                }

                if (blocker != null)
                {
                    if (blocker.Kind != VegetationBlockerKind.GroundRoad &&
                        blocker.Kind != VegetationBlockerKind.BridgeDeck)
                    {
                        resolvedHit = default;
                        rejectionReason = blocker.IsWater
                            ? VegetationRejectionReason.Water
                            : VegetationRejectionReason.Blocked;
                        return false;
                    }

                    PendingBlockers[pendingBlockerCount] = blocker;
                    PendingBlockerHits[pendingBlockerCount] = hit;
                    pendingBlockerCount++;
                    continue;
                }

                if (surface == null)
                {
                    continue;
                }

                if (!surface.Allows(channel))
                {
                    resolvedHit = default;
                    rejectionReason = VegetationRejectionReason.Blocked;
                    return false;
                }

                for (int blockerIndex = 0;
                     blockerIndex < pendingBlockerCount;
                     blockerIndex++)
                {
                    VegetationBlocker pendingBlocker =
                        PendingBlockers[blockerIndex];
                    if (pendingBlocker.BlocksSurface(
                            PendingBlockerHits[blockerIndex].point.y,
                            hit.point.y))
                    {
                        resolvedHit = default;
                        rejectionReason = pendingBlocker.IsWater
                            ? VegetationRejectionReason.Water
                            : VegetationRejectionReason.Blocked;
                        return false;
                    }
                }

                resolvedHit = new VegetationResolvedHit(hit, surface);
                rejectionReason = VegetationRejectionReason.None;
                return true;
            }

            resolvedHit = default;
            rejectionReason = pendingBlockerCount > 0
                ? VegetationRejectionReason.Blocked
                : VegetationRejectionReason.NoSurface;
            return false;
        }

        private static void SortHitsByDistance(int count)
        {
            for (int index = 1; index < count; index++)
            {
                RaycastHit current = Hits[index];
                int insertionIndex = index - 1;
                while (insertionIndex >= 0 &&
                       Hits[insertionIndex].distance > current.distance)
                {
                    Hits[insertionIndex + 1] = Hits[insertionIndex];
                    insertionIndex--;
                }

                Hits[insertionIndex + 1] = current;
            }
        }
    }
}
