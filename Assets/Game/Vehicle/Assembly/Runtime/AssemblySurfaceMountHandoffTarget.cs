using System;
using System.Linq;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Routes a carried part only to a compatible mount owned by the exact
    /// assembly surface under the crosshair. The ray gate is deliberately
    /// friendlier than the donor marker without turning the whole car into
    /// one accidental installation trigger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblySurfaceMountHandoffTarget : MonoBehaviour,
        IMountHandoffTarget
    {
        private const float DefaultMaximumAimRayDistanceMeters = 0.18f;
        private const float DiscBrakeMaximumAimRayDistanceMeters = 0.22f;
        private const float RoadWheelMaximumAimRayDistanceMeters = 0.28f;

        [SerializeField] private VehicleAssemblyController controller;
        [SerializeField] private PartInstance ownerPart;

        private string prompt = "Установить деталь";

        public string HandoffPrompt => prompt;

        public void Configure(
            VehicleAssemblyController assemblyController,
            PartInstance authoredOwnerPart)
        {
            controller = assemblyController;
            ownerPart = authoredOwnerPart;
        }

        public bool CanAccept(
            IPickupTarget pickupTarget,
            in InteractionContext context) =>
            TryResolveMount(pickupTarget, context, out _, updatePrompt: true);

        public void Accept(
            IPickupTarget pickupTarget,
            in InteractionContext context)
        {
            if (!TryResolveMount(
                    pickupTarget,
                    context,
                    out MountPointAuthoring mount,
                    updatePrompt: true))
            {
                return;
            }

            AssemblyOperationResult result = controller
                .BeginInstallFromHandoff(pickupTarget, mount);
            prompt = result.Message;
        }

        private bool TryResolveMount(
            IPickupTarget pickupTarget,
            in InteractionContext context,
            out MountPointAuthoring mount,
            bool updatePrompt)
        {
            mount = null;
            if (controller == null || ownerPart == null ||
                ownerPart.Definition == null)
            {
                if (updatePrompt)
                {
                    prompt = "Поверхность сборки не настроена";
                }

                return false;
            }

            PartInstance part = controller.ResolvePart(pickupTarget);
            if (part == null || part.Definition == null)
            {
                if (updatePrompt)
                {
                    prompt = "В руках нет детали для установки";
                }

                return false;
            }

            MountPointAuthoring bestMount = null;
            MountPointAuthoring bestRejectedMount = null;
            AssemblyOperationResult bestRejection = default;
            float bestAimDistance = float.PositiveInfinity;
            float bestPartDistance = float.PositiveInfinity;
            float bestRejectedAimDistance = float.PositiveInfinity;
            MountPointAuthoring[] candidates = controller.MountPoints;
            for (int index = 0; index < candidates.Length; index++)
            {
                MountPointAuthoring candidate = candidates[index];
                if (candidate == null || candidate.Definition == null ||
                    !IsMountRoutedThroughSurface(candidate) ||
                    !part.Definition.IsCompatibleWith(candidate.Definition))
                {
                    continue;
                }

                float aimDistance = DistanceToForwardRay(
                    candidate.Pose.position,
                    context.Origin,
                    context.Direction);
                if (aimDistance > ResolveMaximumAimRayDistance(candidate))
                {
                    continue;
                }

                AssemblyOperationResult result = controller
                    .EvaluateHandoffInstall(part, candidate);
                if (!result.Succeeded)
                {
                    if (bestRejectedMount == null ||
                        aimDistance < bestRejectedAimDistance)
                    {
                        bestRejectedMount = candidate;
                        bestRejectedAimDistance = aimDistance;
                        bestRejection = result;
                    }

                    continue;
                }

                float partDistance = Vector3.Distance(
                    part.transform.position,
                    candidate.Pose.position);
                if (bestMount == null ||
                    aimDistance < bestAimDistance - 0.0001f ||
                    Mathf.Abs(aimDistance - bestAimDistance) <= 0.0001f &&
                    (partDistance < bestPartDistance - 0.0001f ||
                     Mathf.Abs(partDistance - bestPartDistance) <= 0.0001f &&
                     string.CompareOrdinal(
                         candidate.MountId,
                         bestMount.MountId) < 0))
                {
                    bestMount = candidate;
                    bestAimDistance = aimDistance;
                    bestPartDistance = partDistance;
                }
            }

            if (bestMount == null)
            {
                if (updatePrompt)
                {
                    prompt = bestRejectedMount != null
                        ? bestRejection.Message
                        : "Нет подходящей точки установки под прицелом";
                }

                return false;
            }

            mount = bestMount;
            if (updatePrompt)
            {
                prompt = BuildInstallPrompt(part);
            }

            return true;
        }

        private bool IsMountRoutedThroughSurface(
            MountPointAuthoring candidate)
        {
            if (candidate == null || candidate.Definition == null ||
                ownerPart == null || ownerPart.Definition == null)
            {
                return false;
            }

            if (string.Equals(
                    candidate.Definition.OwnerPartDefinitionId,
                    ownerPart.Definition.DefinitionId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            // A nested donor socket can live in a moving IK/runtime rig while
            // its visible installation surface is the already installed
            // prerequisite part. Route through that exact occupied mount ID;
            // this keeps the trigger usable through spindle/disc geometry
            // without making the whole body shell one enormous install zone.
            return ownerPart.IsInstalled &&
                !string.IsNullOrEmpty(ownerPart.RuntimeState.InstalledMountId) &&
                candidate.Definition.RequiredOccupiedMountIds.Contains(
                    ownerPart.RuntimeState.InstalledMountId,
                    StringComparer.Ordinal);
        }

        private static float DistanceToForwardRay(
            Vector3 point,
            Vector3 origin,
            Vector3 direction)
        {
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            float depth = Vector3.Dot(point - origin, normalizedDirection);
            if (depth < 0f)
            {
                return float.PositiveInfinity;
            }

            Vector3 nearest = origin + normalizedDirection * depth;
            return Vector3.Distance(point, nearest);
        }

        private static float ResolveMaximumAimRayDistance(
            MountPointAuthoring candidate)
        {
            string mountId = candidate?.Definition?.DefinitionId ??
                string.Empty;
            if (mountId.StartsWith(
                    "mount.satsuma.wheel",
                    StringComparison.Ordinal))
            {
                return RoadWheelMaximumAimRayDistanceMeters;
            }

            if (mountId.StartsWith(
                    "mount.satsuma.discbrake-",
                    StringComparison.Ordinal))
            {
                return DiscBrakeMaximumAimRayDistanceMeters;
            }

            return DefaultMaximumAimRayDistanceMeters;
        }

        private static string BuildInstallPrompt(PartInstance part) =>
            part?.Definition != null
                ? "Установить: " + part.Definition.DisplayName
                : "Установить деталь";
    }
}
