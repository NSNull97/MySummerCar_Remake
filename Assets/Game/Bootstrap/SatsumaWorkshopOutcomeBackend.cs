using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Services;
using MSC.Vehicle;
using UnityEngine;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Bounded production bridge for the seven donor Fleetari body-repair
    /// outcomes. Other workshop mutations remain deferred until their owning
    /// vehicle systems exist; this class never reports a partial basket done.
    /// </summary>
    public sealed class SatsumaWorkshopOutcomeBackend : IWorkshopOutcomeBackend
    {
        private const string BodyRepairOfferId =
            "service.workshop.body-repair";

        private static readonly IReadOnlyDictionary<string, string>
            SurfaceByRepairOffer = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                [BodyRepairOfferId] = SatsumaPaintSurfaceIds.Body,
                ["service.workshop.door-left"] = SatsumaPaintSurfaceIds.DoorLeft,
                ["service.workshop.door-right"] = SatsumaPaintSurfaceIds.DoorRight,
                ["service.workshop.fender-left"] = SatsumaPaintSurfaceIds.FenderLeft,
                ["service.workshop.fender-right"] = SatsumaPaintSurfaceIds.FenderRight,
                ["service.workshop.hood"] = SatsumaPaintSurfaceIds.Hood,
                ["service.workshop.bootlid"] = SatsumaPaintSurfaceIds.Bootlid,
            };

        // Donor Bodyfix paints the repaired shell this fixed colour.
        private static readonly Color BodyRepairColor = new(
            0.6544118f,
            0.651757f,
            0.615917f,
            1f);

        // Donor Bodyfix independently chooses one of these twelve colours for
        // every repaired detachable panel. Values are frozen from the FSM.
        private static readonly Color[] PanelRepairColors =
        {
            new(0.1607843f, 0.6470588f, 0.7647059f, 1f),
            new(0.7647059f, 0.5176471f, 0.1372549f, 1f),
            new(0.4470588f, 0.4078431f, 0.09019608f, 1f),
            new(0.6196079f, 0.6784314f, 0.6941177f, 1f),
            new(0.8078431f, 0.7686275f, 0.2392157f, 1f),
            new(0.01176471f, 0.1490196f, 0.2705882f, 1f),
            new(0.4627451f, 0.2941177f, 0.1960784f, 1f),
            new(0.7921569f, 0.7882353f, 0.7647059f, 1f),
            new(0.7921569f, 0.03529412f, 0f, 1f),
            new(0.9647059f, 0.9647059f, 0.9647059f, 1f),
            new(0.9176471f, 0.8509804f, 0.5254902f, 1f),
            new(0.1960784f, 0.227451f, 0.1490196f, 1f),
        };

        private readonly Transform vehicleTransform;
        private readonly VehiclePaintStateController paint;
        private readonly string stableVehicleId;
        private readonly Vector3 workshopWorldPosition;

        public SatsumaWorkshopOutcomeBackend(
            GameObject playerVehicle,
            Vector3 configuredWorkshopWorldPosition)
        {
            if (playerVehicle == null)
            {
                throw new ArgumentNullException(nameof(playerVehicle));
            }

            vehicleTransform = playerVehicle.transform;
            paint = playerVehicle.GetComponent<VehiclePaintStateController>() ??
                throw new ArgumentException(
                    "The player Satsuma has no paint-state controller.",
                    nameof(playerVehicle));
            VehiclePersistenceBindingCore persistence = playerVehicle
                .GetComponent<VehiclePersistenceBindingCore>() ??
                throw new ArgumentException(
                    "The player Satsuma has no persistence identity.",
                    nameof(playerVehicle));
            stableVehicleId = persistence.StableVehicleId;
            if (string.IsNullOrWhiteSpace(stableVehicleId))
            {
                throw new ArgumentException(
                    "The player Satsuma has no valid stable vehicle identity.",
                    nameof(playerVehicle));
            }

            workshopWorldPosition = configuredWorkshopWorldPosition;
        }

        public bool IsPlayerVehicleWithin(float distanceMeters)
        {
            return float.IsFinite(distanceMeters) &&
                   distanceMeters >= 0f &&
                   Vector3.SqrMagnitude(
                       vehicleTransform.position - workshopWorldPosition) <=
                   distanceMeters * distanceMeters;
        }

        // The Ferndale/loaner world entity is not owned by this bounded bridge.
        public bool IsLoanerWithin(float distanceMeters) => false;

        public bool TryApply(
            in WorkshopOutcomeRequest request,
            out string failure)
        {
            if (string.IsNullOrWhiteSpace(request.OrderId) ||
                request.OfferIds == null ||
                request.OfferIds.Length == 0)
            {
                failure = "The Fleetari outcome request is empty.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(request.PlayerVehicleStableId) &&
                !string.Equals(
                    request.PlayerVehicleStableId,
                    stableVehicleId,
                    StringComparison.Ordinal))
            {
                failure = "The Fleetari order targets another vehicle.";
                return false;
            }

            var requestedSurfaces = new List<(string offerId, string surfaceId)>();
            var uniqueSurfaces = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < request.OfferIds.Length; index++)
            {
                string offerId = request.OfferIds[index] ?? string.Empty;
                if (!SurfaceByRepairOffer.TryGetValue(
                        offerId,
                        out string surfaceId))
                {
                    failure =
                        $"Workshop outcome '{offerId}' is not implemented by " +
                        "the bounded Satsuma body-repair bridge.";
                    return false;
                }

                if (uniqueSurfaces.Add(surfaceId))
                {
                    requestedSurfaces.Add((offerId, surfaceId));
                }
            }

            VehiclePaintMaterialProfile regularProfile =
                paint.PaintMaterialProfiles.SingleOrDefault(value =>
                    value != null &&
                    value.PaintType == VehiclePaintType.Regular);
            if (regularProfile?.Material == null)
            {
                failure = "The Satsuma regular-paint material is unavailable.";
                return false;
            }

            // Preflight every target before touching any renderer. This keeps a
            // mixed/invalid order from leaving half of the car repaired.
            foreach ((string _, string surfaceId) in requestedSurfaces)
            {
                VehiclePaintSurfaceBinding binding = paint.PaintSurfaceBindings
                    .SingleOrDefault(value =>
                        value != null &&
                        string.Equals(
                            value.SurfaceId,
                            surfaceId,
                            StringComparison.Ordinal));
                if (binding?.Renderer == null ||
                    binding.MaterialIndices == null ||
                    binding.MaterialIndices.Count == 0)
                {
                    failure =
                        $"Satsuma paint surface '{surfaceId}' is unavailable.";
                    return false;
                }
            }

            foreach ((string offerId, string surfaceId) in requestedSurfaces)
            {
                int paletteIndex;
                Color repairColor;
                if (string.Equals(
                        offerId,
                        BodyRepairOfferId,
                        StringComparison.Ordinal))
                {
                    paletteIndex = 0;
                    repairColor = BodyRepairColor;
                }
                else
                {
                    paletteIndex = ResolvePanelColorIndex(
                        request.OrderId,
                        surfaceId);
                    repairColor = PanelRepairColors[paletteIndex];
                }

                if (!paint.TryApplySurfacePaint(
                        surfaceId,
                        paletteIndex,
                        repairColor,
                        VehiclePaintType.Regular,
                        out failure))
                {
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        public bool TryRelocatePlayerVehicleForOverdueLoaner(
            out string failure)
        {
            failure =
                "The Fleetari loaner/vehicle relocation outcome is not implemented.";
            return false;
        }

        internal static int ResolvePanelColorIndex(
            string orderId,
            string surfaceId)
        {
            // Unlike System.String.GetHashCode this is stable across processes,
            // so replay after a save cannot choose a second random paint colour.
            unchecked
            {
                uint hash = 2166136261;
                string value = (orderId ?? string.Empty) + "\u001f" +
                               (surfaceId ?? string.Empty);
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619;
                }

                return (int)(hash % PanelRepairColors.Length);
            }
        }
    }
}
