using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Services
{
    [Serializable]
    public sealed class ServiceAvailabilityWindow
    {
        [SerializeField, Range(0, 127)] private int dayMask;
        [SerializeField, Range(0, 1439)] private int startMinute;
        [SerializeField, Range(0, 1440)] private int endMinute;

        public int DayMask => dayMask;
        public int StartMinute => startMinute;
        public int EndMinute => endMinute;

        public void ConfigureForAuthoring(
            int configuredDayMask,
            int configuredStartMinute,
            int configuredEndMinute)
        {
            dayMask = configuredDayMask;
            startMinute = configuredStartMinute;
            endMinute = configuredEndMinute;
        }

        public bool Contains(DayOfWeek day, int minuteOfDay)
        {
            if (minuteOfDay < 0 || minuteOfDay >= 1440)
            {
                return false;
            }

            int bit = 1 << (int)day;
            if (startMinute < endMinute)
            {
                return (dayMask & bit) != 0 &&
                       minuteOfDay >= startMinute &&
                       minuteOfDay < endMinute;
            }

            if (startMinute == endMinute)
            {
                return (dayMask & bit) != 0;
            }

            if (minuteOfDay >= startMinute)
            {
                return (dayMask & bit) != 0;
            }

            DayOfWeek previous = (DayOfWeek)(((int)day + 6) % 7);
            return (dayMask & (1 << (int)previous)) != 0 &&
                   minuteOfDay < endMinute;
        }
    }

    [Serializable]
    public sealed class ServiceLocationDefinition
    {
        [SerializeField] private string locationId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string sourceStableId = string.Empty;
        [SerializeField] private ServiceLocationKind kind;
        [SerializeField] private string interactionAnchorId = string.Empty;
        [SerializeField] private string handoffAnchorId = string.Empty;
        [SerializeField] private Vector3 worldPosition;
        [SerializeField] private Vector3 handoffWorldPosition;
        [SerializeField] private ServiceAvailabilityWindow[] availability =
            Array.Empty<ServiceAvailabilityWindow>();

        public string LocationId => locationId;
        public string DisplayName => displayName;
        public string SourceStableId => sourceStableId;
        public ServiceLocationKind Kind => kind;
        public string InteractionAnchorId => interactionAnchorId;
        public string HandoffAnchorId => handoffAnchorId;
        public Vector3 WorldPosition => worldPosition;
        public Vector3 HandoffWorldPosition => handoffWorldPosition;
        public IReadOnlyList<ServiceAvailabilityWindow> Availability => availability;

        public void ConfigureForAuthoring(
            string configuredLocationId,
            string configuredDisplayName,
            string configuredSourceStableId,
            ServiceLocationKind configuredKind,
            string configuredInteractionAnchorId,
            string configuredHandoffAnchorId,
            Vector3 configuredWorldPosition,
            ServiceAvailabilityWindow[] configuredAvailability)
        {
            ConfigureForAuthoring(
                configuredLocationId,
                configuredDisplayName,
                configuredSourceStableId,
                configuredKind,
                configuredInteractionAnchorId,
                configuredHandoffAnchorId,
                configuredWorldPosition,
                configuredWorldPosition,
                configuredAvailability);
        }

        public void ConfigureForAuthoring(
            string configuredLocationId,
            string configuredDisplayName,
            string configuredSourceStableId,
            ServiceLocationKind configuredKind,
            string configuredInteractionAnchorId,
            string configuredHandoffAnchorId,
            Vector3 configuredWorldPosition,
            Vector3 configuredHandoffWorldPosition,
            ServiceAvailabilityWindow[] configuredAvailability)
        {
            locationId = configuredLocationId ?? string.Empty;
            displayName = configuredDisplayName ?? string.Empty;
            sourceStableId = configuredSourceStableId ?? string.Empty;
            kind = configuredKind;
            interactionAnchorId = configuredInteractionAnchorId ?? string.Empty;
            handoffAnchorId = configuredHandoffAnchorId ?? string.Empty;
            worldPosition = configuredWorldPosition;
            handoffWorldPosition = configuredHandoffWorldPosition;
            availability = configuredAvailability ??
                Array.Empty<ServiceAvailabilityWindow>();
        }
    }

    [Serializable]
    public sealed class ServiceOfferDefinition
    {
        [SerializeField] private string offerId = string.Empty;
        [SerializeField] private string locationId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private ServiceOfferKind kind;
        [SerializeField] private string priceId = string.Empty;
        [SerializeField, Min(0)] private long basePriceMinorUnits;
        [SerializeField] private string itemDefinitionId = string.Empty;
        [SerializeField] private string effectId = string.Empty;
        [SerializeField] private string exclusiveGroupId = string.Empty;
        [SerializeField] private int variantIndex;
        [SerializeField, Min(1)] private int quantityPerUnit = 1;
        [SerializeField, Min(0)] private int stockCapacity;
        [SerializeField, Range(0, 6)] private int restockDayOfWeek =
            (int)DayOfWeek.Thursday;
        [SerializeField] private bool restockable = true;
        [SerializeField] private FuelGrade fuelGrade;

        public string OfferId => offerId;
        public string LocationId => locationId;
        public string DisplayName => displayName;
        public ServiceOfferKind Kind => kind;
        public string PriceId => priceId;
        public long BasePriceMinorUnits => basePriceMinorUnits;
        public string ItemDefinitionId => itemDefinitionId;
        public string EffectId => effectId;
        public string ExclusiveGroupId => exclusiveGroupId;
        public int VariantIndex => variantIndex;
        public int QuantityPerUnit => quantityPerUnit;
        public int StockCapacity => stockCapacity;
        public DayOfWeek RestockDayOfWeek => (DayOfWeek)restockDayOfWeek;
        public bool Restockable => restockable;
        public FuelGrade FuelGrade => fuelGrade;

        public void ConfigureForAuthoring(
            string configuredOfferId,
            string configuredLocationId,
            string configuredDisplayName,
            ServiceOfferKind configuredKind,
            string configuredPriceId,
            string configuredItemDefinitionId = "",
            string configuredEffectId = "",
            string configuredExclusiveGroupId = "",
            int configuredVariantIndex = 0,
            int configuredQuantityPerUnit = 1,
            int configuredStockCapacity = 0,
            DayOfWeek configuredRestockDay = DayOfWeek.Thursday,
            FuelGrade configuredFuelGrade = FuelGrade.Gasoline98,
            long configuredBasePriceMinorUnits = 0,
            bool configuredRestockable = true)
        {
            offerId = configuredOfferId ?? string.Empty;
            locationId = configuredLocationId ?? string.Empty;
            displayName = configuredDisplayName ?? string.Empty;
            kind = configuredKind;
            priceId = configuredPriceId ?? string.Empty;
            basePriceMinorUnits = configuredBasePriceMinorUnits;
            itemDefinitionId = configuredItemDefinitionId ?? string.Empty;
            effectId = configuredEffectId ?? string.Empty;
            exclusiveGroupId = configuredExclusiveGroupId ?? string.Empty;
            variantIndex = configuredVariantIndex;
            quantityPerUnit = configuredQuantityPerUnit;
            stockCapacity = configuredStockCapacity;
            restockDayOfWeek = (int)configuredRestockDay;
            restockable = configuredRestockable;
            fuelGrade = configuredFuelGrade;
        }
    }

    [Serializable]
    public sealed class ServiceFuelPriceDefinition
    {
        [SerializeField] private FuelGrade grade;
        [SerializeField, Min(1)] private long initialMinorUnitsPerLiter = 1;
        // The donor's initial price can sit outside the range used by later
        // price rolls (diesel starts at 4.23 MK/L, subsequent rolls are
        // 3.30-4.05 MK/L). Keep these as separate authored concepts.
        [SerializeField, Min(1)] private long minimumMinorUnitsPerLiter = 1;
        [SerializeField, Min(1)] private long maximumMinorUnitsPerLiter = 1;

        public FuelGrade Grade => grade;
        public long InitialMinorUnitsPerLiter => initialMinorUnitsPerLiter;
        public long MinimumMinorUnitsPerLiter => minimumMinorUnitsPerLiter;
        public long MaximumMinorUnitsPerLiter => maximumMinorUnitsPerLiter;

        public void ConfigureForAuthoring(
            FuelGrade configuredGrade,
            long initial,
            long minimum,
            long maximum)
        {
            grade = configuredGrade;
            initialMinorUnitsPerLiter = initial;
            minimumMinorUnitsPerLiter = minimum;
            maximumMinorUnitsPerLiter = maximum;
        }
    }

    [CreateAssetMenu(
        fileName = "ServiceCatalog",
        menuName = "MSC/Services/Service Catalog")]
    public sealed class ServiceCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId = string.Empty;
        [SerializeField] private string donorSceneSha256 = string.Empty;
        [SerializeField] private ServiceLocationDefinition[] locations =
            Array.Empty<ServiceLocationDefinition>();
        [SerializeField] private ServiceOfferDefinition[] offers =
            Array.Empty<ServiceOfferDefinition>();
        [SerializeField] private ServiceFuelPriceDefinition[] fuelPrices =
            Array.Empty<ServiceFuelPriceDefinition>();

        private Dictionary<string, ServiceLocationDefinition> locationsById;
        private Dictionary<string, ServiceOfferDefinition> offersById;
        private Dictionary<FuelGrade, ServiceFuelPriceDefinition> fuelByGrade;

        public string CatalogId => catalogId;
        public string DonorSceneSha256 => donorSceneSha256;
        public IReadOnlyList<ServiceLocationDefinition> Locations => locations;
        public IReadOnlyList<ServiceOfferDefinition> Offers => offers;
        public IReadOnlyList<ServiceFuelPriceDefinition> FuelPrices => fuelPrices;

        public void ConfigureForAuthoring(
            string configuredCatalogId,
            string configuredDonorSceneSha256,
            ServiceLocationDefinition[] configuredLocations,
            ServiceOfferDefinition[] configuredOffers,
            ServiceFuelPriceDefinition[] configuredFuelPrices)
        {
            catalogId = configuredCatalogId ?? string.Empty;
            donorSceneSha256 = configuredDonorSceneSha256 ?? string.Empty;
            locations = configuredLocations ??
                Array.Empty<ServiceLocationDefinition>();
            offers = configuredOffers ?? Array.Empty<ServiceOfferDefinition>();
            fuelPrices = configuredFuelPrices ??
                Array.Empty<ServiceFuelPriceDefinition>();
            locationsById = null;
            offersById = null;
            fuelByGrade = null;
        }

        public bool TryGetLocation(
            string locationId,
            out ServiceLocationDefinition definition)
        {
            EnsureIndices();
            return locationsById.TryGetValue(
                locationId ?? string.Empty,
                out definition);
        }

        public bool TryGetOffer(
            string offerId,
            out ServiceOfferDefinition definition)
        {
            EnsureIndices();
            return offersById.TryGetValue(
                offerId ?? string.Empty,
                out definition);
        }

        public bool TryGetFuelPrice(
            FuelGrade grade,
            out ServiceFuelPriceDefinition definition)
        {
            EnsureIndices();
            return fuelByGrade.TryGetValue(grade, out definition);
        }

        public bool TryValidate(out string failure)
        {
            if (!ServiceStableId.IsCanonical(catalogId) ||
                donorSceneSha256 == null || donorSceneSha256.Length != 64 ||
                locations == null || locations.Length == 0 ||
                offers == null || offers.Length == 0)
            {
                failure = "Service catalog metadata or collections are invalid.";
                return false;
            }

            var locationIds = new HashSet<string>(StringComparer.Ordinal);
            var locationKinds =
                new Dictionary<string, ServiceLocationKind>(StringComparer.Ordinal);
            foreach (ServiceLocationDefinition location in locations)
            {
                if (location == null ||
                    !ServiceStableId.IsCanonical(location.LocationId) ||
                    !ServiceStableId.IsCanonical(location.SourceStableId) ||
                    !ServiceStableId.IsCanonical(location.InteractionAnchorId) ||
                    !ServiceStableId.IsCanonical(location.HandoffAnchorId) ||
                    string.IsNullOrWhiteSpace(location.DisplayName) ||
                    !Enum.IsDefined(typeof(ServiceLocationKind), location.Kind) ||
                    !IsFinite(location.WorldPosition) ||
                    !IsFinite(location.HandoffWorldPosition) ||
                    location.Availability == null ||
                    location.Availability.Count == 0 ||
                    !locationIds.Add(location.LocationId))
                {
                    failure = "Service location metadata is invalid or duplicated.";
                    return false;
                }

                locationKinds.Add(location.LocationId, location.Kind);

                foreach (ServiceAvailabilityWindow window in location.Availability)
                {
                    if (window == null || window.DayMask <= 0 ||
                        window.DayMask > 127 || window.StartMinute < 0 ||
                        window.StartMinute >= 1440 || window.EndMinute < 0 ||
                        window.EndMinute > 1440)
                    {
                        failure = $"Service location '{location.LocationId}' has an invalid availability window.";
                        return false;
                    }
                }
            }

            var offerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ServiceOfferDefinition offer in offers)
            {
                if (offer == null)
                {
                    failure = "Service offer metadata is invalid or duplicated.";
                    return false;
                }

                bool retail = offer.Kind == ServiceOfferKind.RetailItem;
                bool handoff = retail || offer.Kind == ServiceOfferKind.PubItem;
                bool fuel = offer.Kind == ServiceOfferKind.Fuel;
                bool fixedPrice = offer.Kind == ServiceOfferKind.PubItem ||
                                  offer.Kind == ServiceOfferKind.Workshop ||
                                  offer.Kind == ServiceOfferKind.Inspection;
                bool validHandoff = !handoff ||
                    ServiceStableId.IsCanonical(offer.ItemDefinitionId) ||
                    ServiceStableId.IsCanonical(offer.EffectId);
                if (!ServiceStableId.IsCanonical(offer.OfferId) ||
                    !locationIds.Contains(offer.LocationId) ||
                    string.IsNullOrWhiteSpace(offer.DisplayName) ||
                    !Enum.IsDefined(typeof(ServiceOfferKind), offer.Kind) ||
                    !locationKinds.TryGetValue(
                        offer.LocationId,
                        out ServiceLocationKind locationKind) ||
                    locationKind != ExpectedLocationKind(offer.Kind) ||
                    retail &&
                        (ServiceStableId.IsCanonical(offer.PriceId) ==
                         (offer.BasePriceMinorUnits > 0)) ||
                    fixedPrice && (offer.BasePriceMinorUnits <= 0 ||
                                   !string.IsNullOrEmpty(offer.PriceId)) ||
                    fuel && (offer.BasePriceMinorUnits != 0 ||
                             !string.IsNullOrEmpty(offer.PriceId)) ||
                    !validHandoff ||
                    !string.IsNullOrEmpty(offer.ExclusiveGroupId) &&
                        !ServiceStableId.IsCanonical(offer.ExclusiveGroupId) ||
                    offer.QuantityPerUnit <= 0 || offer.VariantIndex < 0 ||
                    retail && offer.StockCapacity <= 0 ||
                    !retail && offer.StockCapacity != 0 ||
                    !offerIds.Add(offer.OfferId))
                {
                    failure = "Service offer metadata is invalid or duplicated.";
                    return false;
                }
            }

            var grades = new HashSet<FuelGrade>();
            foreach (ServiceFuelPriceDefinition fuel in
                     fuelPrices ?? Array.Empty<ServiceFuelPriceDefinition>())
            {
                if (fuel == null || !Enum.IsDefined(typeof(FuelGrade), fuel.Grade) ||
                    fuel.MinimumMinorUnitsPerLiter <= 0 ||
                    fuel.MaximumMinorUnitsPerLiter <
                        fuel.MinimumMinorUnitsPerLiter ||
                    fuel.InitialMinorUnitsPerLiter <= 0 ||
                    !grades.Add(fuel.Grade))
                {
                    failure = "Service fuel price metadata is invalid or duplicated.";
                    return false;
                }
            }

            if (offers.Any(value => value != null &&
                                    value.Kind == ServiceOfferKind.Fuel) &&
                grades.Count != Enum.GetValues(typeof(FuelGrade)).Length)
            {
                failure = "Fuel offers require a bounded price for every grade.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static ServiceLocationKind ExpectedLocationKind(
            ServiceOfferKind offerKind) => offerKind switch
            {
                ServiceOfferKind.RetailItem => ServiceLocationKind.Store,
                ServiceOfferKind.PubItem => ServiceLocationKind.Pub,
                ServiceOfferKind.Fuel => ServiceLocationKind.FuelStation,
                ServiceOfferKind.Workshop => ServiceLocationKind.Workshop,
                ServiceOfferKind.Inspection => ServiceLocationKind.Inspection,
                _ => throw new ArgumentOutOfRangeException(nameof(offerKind)),
            };

        private void EnsureIndices()
        {
            if (locationsById != null && offersById != null &&
                fuelByGrade != null)
            {
                return;
            }

            locationsById = (locations ??
                    Array.Empty<ServiceLocationDefinition>())
                .Where(value => value != null)
                .ToDictionary(value => value.LocationId, StringComparer.Ordinal);
            offersById = (offers ?? Array.Empty<ServiceOfferDefinition>())
                .Where(value => value != null)
                .ToDictionary(value => value.OfferId, StringComparer.Ordinal);
            fuelByGrade = (fuelPrices ??
                    Array.Empty<ServiceFuelPriceDefinition>())
                .Where(value => value != null)
                .ToDictionary(value => value.Grade);
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
