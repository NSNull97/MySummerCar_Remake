using System;
using MSC.Core.Identity;
using MSC.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Services.Presentation
{
    /// <summary>
    /// Idempotent adapter from paid service lines to project-owned world items.
    /// It deliberately knows nothing about checkout, prices or NPC animations.
    /// </summary>
    public sealed class ServiceItemHandoffBackend :
        IServiceHandoffBackend,
        IServiceHandoffPreflight
    {
        private const string TeimoPubLocationId =
            "service.location.teimo-pub";

        private readonly ServiceCatalog catalog;
        private readonly ItemWorldRuntime items;
        private readonly Scene persistentScene;
        private readonly IServiceEffectHandoffBackend effects;

        public ServiceItemHandoffBackend(
            ServiceCatalog configuredCatalog,
            ItemWorldRuntime configuredItems,
            Scene configuredPersistentScene,
            IServiceEffectHandoffBackend configuredEffects = null)
        {
            catalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
            items = configuredItems ??
                throw new ArgumentNullException(nameof(configuredItems));
            if (!configuredPersistentScene.IsValid() ||
                !configuredPersistentScene.isLoaded)
            {
                throw new ArgumentException(
                    "Service handoff requires a loaded persistent scene.",
                    nameof(configuredPersistentScene));
            }

            persistentScene = configuredPersistentScene;
            effects = configuredEffects;
        }

        public bool TryFulfill(
            in ServiceHandoffRequest request,
            out string failure)
        {
            if (!TryValidateRequest(
                    in request,
                    out ServiceLocationDefinition location,
                    out _,
                    out failure))
            {
                return false;
            }

            if (!CanFulfill(in request, out failure))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(request.ItemDefinitionId) &&
                !TrySpawnItems(
                    in request,
                    location.HandoffWorldPosition,
                    out failure))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(request.EffectId) &&
                !effects.TryApply(
                    request.OperationId,
                    request.LineIndex,
                    request.EffectId,
                    request.Quantity,
                    out failure))
            {
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public bool CanFulfill(
            in ServiceHandoffRequest request,
            out string failure)
        {
            if (!TryValidateRequest(
                    in request,
                    out _,
                    out _,
                    out failure))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(request.ItemDefinitionId) &&
                !items.Definitions.TryGet(request.ItemDefinitionId, out _))
            {
                failure =
                    $"Item definition '{request.ItemDefinitionId}' is unavailable.";
                return false;
            }

            if (!string.IsNullOrEmpty(request.EffectId) && effects == null)
            {
                failure = $"No effect backend can apply '{request.EffectId}'.";
                return false;
            }

            if (!string.IsNullOrEmpty(request.EffectId) &&
                effects is IServiceEffectHandoffPreflight preflight &&
                !preflight.CanApply(
                    request.EffectId,
                    request.Quantity,
                    out failure))
            {
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private bool TryValidateRequest(
            in ServiceHandoffRequest request,
            out ServiceLocationDefinition location,
            out ServiceOfferDefinition offer,
            out string failure)
        {
            // Both lookups participate in a short-circuit expression. Assign
            // the outputs up front so a missing location cannot leave the
            // offer output unassigned.
            location = null;
            offer = null;
            if (!catalog.TryGetLocation(request.LocationId, out location) ||
                !catalog.TryGetOffer(request.OfferId, out offer) ||
                request.LineIndex < 0 || request.Quantity <= 0 ||
                !string.Equals(
                    offer.LocationId,
                    location.LocationId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.HandoffAnchorId,
                    location.HandoffAnchorId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.ItemDefinitionId,
                    offer.ItemDefinitionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.EffectId,
                    offer.EffectId,
                    StringComparison.Ordinal) ||
                request.VariantIndex != offer.VariantIndex ||
                request.Quantity % offer.QuantityPerUnit != 0 ||
                (offer.Kind != ServiceOfferKind.RetailItem &&
                 offer.Kind != ServiceOfferKind.PubItem) ||
                (string.IsNullOrEmpty(request.ItemDefinitionId) &&
                 string.IsNullOrEmpty(request.EffectId)))
            {
                failure = "Service handoff request is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private bool TrySpawnItems(
            in ServiceHandoffRequest request,
            Vector3 position,
            out string failure)
        {
            if (!items.Definitions.TryGet(
                    request.ItemDefinitionId,
                    out _))
            {
                failure =
                    $"Item definition '{request.ItemDefinitionId}' is unavailable.";
                return false;
            }

            Quaternion spawnRotation =
                ServiceHandoffPlacementPolicy.GetCounterRotation(
                    request.LocationId,
                    request.ItemDefinitionId);
            for (int index = 0; index < request.Quantity; index++)
            {
                StableEntityId stableId = ItemStableIdUtility.CreateDeterministic(
                    $"services|{request.OperationId}|{request.LineIndex}|{index}");
                if (items.TryGetInstance(stableId.Value, out _))
                {
                    continue;
                }

                Vector3 spawnPosition = ResolveSpawnPosition(
                    request.LocationId,
                    request.ItemDefinitionId,
                    position,
                    request.LineIndex,
                    index);

                try
                {
                    items.SpawnDynamic(
                        request.ItemDefinitionId,
                        stableId,
                        spawnPosition,
                        spawnRotation,
                        persistentScene,
                        variantIndex: request.VariantIndex);
                }
                catch (InvalidOperationException exception)
                {
                    failure = exception.Message;
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static Vector3 ResolveSpawnPosition(
            string locationId,
            string definitionId,
            Vector3 fallbackPosition,
            int lineIndex,
            int itemIndex)
        {
            // Exact donor pub points already describe the spawned object's
            // pivot. Adding a proxy half-height or settling the collider here
            // lifts the delivered item above Teimo's counter.
            return ServiceHandoffPlacementPolicy.GetCounterPosition(
                       locationId,
                       definitionId,
                       fallbackPosition) +
                   SpawnOffset(locationId, lineIndex, itemIndex);
        }

        private static Vector3 SpawnOffset(
            string locationId,
            int lineIndex,
            int itemIndex)
        {
            if (string.Equals(
                    locationId,
                    TeimoPubLocationId,
                    StringComparison.Ordinal))
            {
                // Item zero uses the exact donor spawn point. Extra units are
                // only separated when an offer explicitly creates multiples.
                return new Vector3(
                    (itemIndex % 3) * 0.12f,
                    (itemIndex / 3) * 0.04f,
                    (itemIndex / 3) * 0.1f);
            }

            int lineColumn = Mathf.Max(0, lineIndex) % 4;
            int lineRow = Mathf.Max(0, lineIndex) / 4;
            int itemColumn = itemIndex % 3;
            int itemRow = itemIndex / 3;
            return new Vector3(
                (lineColumn - 1.5f) * 0.28f +
                (itemColumn - 1f) * 0.08f,
                0.28f + itemRow * 0.04f,
                lineRow * 0.24f + itemRow * 0.1f);
        }
    }
}
