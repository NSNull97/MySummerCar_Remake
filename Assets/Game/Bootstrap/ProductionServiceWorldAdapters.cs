using System;
using MSC.Characters;
using MSC.NPC;
using MSC.Services;
using UnityEngine;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Binds service staffing to the authoritative NPC simulation without
    /// inspecting donor presentation hierarchy or streamed GameObjects.
    /// </summary>
    public sealed class ProductionNpcServiceAvailabilitySource :
        IServiceAvailabilitySource
    {
        private const string TeimoDefinitionId =
            "character.fixture.stationary-service";
        private const string FleetariDefinitionId = "character.fleetari";
        private const string InspectionOfficerDefinitionId =
            "character.inspection-officer";
        private const string TeimoShopAnchorId =
            "anchor.fixture.stationary-service";
        private const string TeimoPubAnchorId = "anchor.teimo.pub";
        private const string FleetariWorkshopAnchorId =
            "anchor.fleetari.repair-shop";
        private const string InspectionStationAnchorId =
            "anchor.inspection-officer.station";

        private readonly NpcWorldRuntime npc;

        public ProductionNpcServiceAvailabilitySource(
            NpcWorldRuntime configuredNpc)
        {
            npc = configuredNpc ??
                throw new ArgumentNullException(nameof(configuredNpc));
            if (!npc.IsInitialized)
            {
                throw new ArgumentException(
                    "Service staffing requires an initialized NPC runtime.",
                    nameof(configuredNpc));
            }
        }

        public bool IsLocationStaffed(string locationId)
        {
            return locationId switch
            {
                "service.location.teimo-store" => IsStaffedAt(
                    TeimoDefinitionId,
                    TeimoShopAnchorId),
                "service.location.teimo-pub" => IsStaffedAt(
                    TeimoDefinitionId,
                    TeimoPubAnchorId),
                "service.location.teimo-fuel" => IsStaffedAt(
                    TeimoDefinitionId,
                    TeimoShopAnchorId),
                "service.location.teimo-fuel-diesel" => IsStaffedAt(
                    TeimoDefinitionId,
                    TeimoShopAnchorId),
                "service.location.teimo-fuel-oil" => IsStaffedAt(
                    TeimoDefinitionId,
                    TeimoShopAnchorId),
                "service.location.workshop.fleetari" => IsStaffedAt(
                    FleetariDefinitionId,
                    FleetariWorkshopAnchorId),
                "service.location.inspection-station" => IsStaffedAt(
                    InspectionOfficerDefinitionId,
                    InspectionStationAnchorId),
                _ => false,
            };
        }

        private bool IsStaffedAt(
            string characterDefinitionId,
            string anchorId)
        {
            if (!npc.TryGetInstance(
                    characterDefinitionId,
                    out CharacterInstance character) ||
                !string.Equals(
                    character.CurrentAnchorId,
                    anchorId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return character.ActivityState == CharacterActivityState.Idle ||
                   character.ActivityState == CharacterActivityState.Working ||
                   character.ActivityState == CharacterActivityState.Talking;
        }
    }

    /// <summary>
    /// Resolves service proximity against authored world coordinates. The
    /// Services module remains independent from player and streaming objects.
    /// </summary>
    public sealed class ProductionPlayerServiceProximitySource :
        IServiceProximitySource
    {
        private readonly ServiceCatalog catalog;
        private readonly Transform player;

        public ProductionPlayerServiceProximitySource(
            ServiceCatalog configuredCatalog,
            Transform configuredPlayer)
        {
            catalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
            player = configuredPlayer ??
                throw new ArgumentNullException(nameof(configuredPlayer));
        }

        public bool IsPlayerWithin(string locationId, float distanceMeters)
        {
            if (!float.IsFinite(distanceMeters) || distanceMeters < 0f ||
                !catalog.TryGetLocation(
                    locationId,
                    out ServiceLocationDefinition location))
            {
                return false;
            }

            return Vector3.Distance(
                       player.position,
                       location.WorldPosition) <= distanceMeters;
        }
    }
}
