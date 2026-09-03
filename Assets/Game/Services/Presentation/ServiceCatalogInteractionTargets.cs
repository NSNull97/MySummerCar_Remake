using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Services.Presentation
{
    public interface IServiceCatalogBookPresenter
    {
        bool IsOpen { get; }

        bool TryOpen();
    }

    public readonly struct ServiceCatalogBookEntry
    {
        public ServiceCatalogBookEntry(
            string id,
            string displayName,
            long priceMinorUnits)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PriceMinorUnits = priceMinorUnits;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public long PriceMinorUnits { get; }
    }

    /// <summary>
    /// Project-owned state boundary for a physical service catalog. Temporary
    /// donor page art is presentation only and never owns selections or orders.
    /// </summary>
    public interface IServiceCatalogBookSelection
    {
        int EntryCount { get; }
        int CurrentIndex { get; }
        int SelectedCount { get; }
        long SelectedTotalMinorUnits { get; }

        ServiceCatalogBookEntry GetEntry(int index);
        bool IsEntrySelected(int index);
        bool TryActivateEntry(int index, float variant = float.NaN);
        bool TryConfirm();
    }

    /// <summary>
    /// Fleetari's physical service brochure. Mouse wheel browses the complete
    /// catalog and primary interaction toggles the visible service in the
    /// authoritative, save-backed workshop selection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FleetariWorkshopCatalogInteractionTarget :
        ServiceInteractionTargetBase,
        IToolActivationTarget,
        IServiceCatalogBookSelection
    {
        private static readonly string[] BrochureOfferOrder =
        {
            "service.workshop.toe-alignment",
            "service.workshop.brakes",
            "service.workshop.engine-repair",
            "service.workshop.engine-adjustment",
            "service.workshop.engine-tune",
            "service.workshop.windshield",
            "service.workshop.suspension",
            "service.workshop.rollcage-install",
            "service.workshop.rollcage-remove",
            "service.workshop.n2o-bottle-fill",
            "service.workshop.final-gear",
            "service.workshop.body-repair",
            "service.workshop.door-left",
            "service.workshop.door-right",
            "service.workshop.fender-left",
            "service.workshop.fender-right",
            "service.workshop.hood",
            "service.workshop.bootlid",
            "service.workshop.bumper-front",
            "service.workshop.bumper-rear",
            "service.workshop.grille",
            "service.workshop.paint-regular",
            "service.workshop.paint-metallic",
            "service.workshop.paint-art",
            "service.workshop.paint-gt",
            "service.workshop.rim-polish",
            "service.workshop.rim-regular",
            "service.workshop.rim-metallic",
            "service.workshop.tires-standard",
            "service.workshop.tires-gommer-gobra",
            "service.workshop.tires-europeiska",
            "service.workshop.tires-sutasiko",
        };

        private readonly List<ServiceOfferDefinition> offers = new();
        private IServiceCatalogBookPresenter bookPresenter;
        private int currentIndex;

        protected override ServiceLocationKind ExpectedLocationKind =>
            ServiceLocationKind.Workshop;

        public int OfferCount => offers.Count;
        public int EntryCount => offers.Count;
        public int CurrentIndex => currentIndex;
        public int SelectedCount => Runtime == null
            ? 0
            : (Runtime.CaptureWorkshopSelection().offerIds ??
               Array.Empty<string>()).Length;
        public long SelectedTotalMinorUnits
        {
            get
            {
                if (Runtime != null && Runtime.TryQuoteWorkshop(
                        out WorkshopPriceQuote quote,
                        out _))
                {
                    return quote.PayableMinorUnits;
                }

                if (Runtime == null)
                {
                    return 0L;
                }

                HashSet<string> selected = new(
                    Runtime.CaptureWorkshopSelection().offerIds ??
                    Array.Empty<string>(),
                    StringComparer.Ordinal);
                return offers
                    .Where(offer => selected.Contains(offer.OfferId))
                    .Sum(offer => offer.BasePriceMinorUnits);
            }
        }
        public string CurrentOfferId => CurrentOffer?.OfferId ?? string.Empty;

        public override string InteractionDisplayName
        {
            get
            {
                ServiceOfferDefinition offer = CurrentOffer;
                if (offer == null)
                {
                    return "Каталог Флитари";
                }

                bool selected = IsSelected(offer.OfferId);
                return $"{currentIndex + 1}/{offers.Count}  " +
                       $"{offer.DisplayName} — {FormatMoney(offer.BasePriceMinorUnits)} MK " +
                       (selected ? "[выбрано]" : string.Empty);
            }
        }

        public override string InteractionPrompt => CurrentOffer == null
            ? "Каталог пуст"
            : "Открыть каталог";

        public string ToolPrompt => "Открыть каталог";

        public bool CanActivateTool(in InteractionContext context) =>
            CanInteract(context);

        public void ActivateTool(in InteractionContext context) =>
            Interact(context);

        public void ConfigureForAuthoring(
            string configuredLocationId,
            string configuredInteractionAnchorId) =>
            ConfigureBindingForAuthoring(
                configuredLocationId,
                configuredInteractionAnchorId);

        public void Bind(
            ServiceRuntime configuredRuntime,
            ServiceInteractionFeedbackHandler feedback = null)
        {
            BindRuntime(configuredRuntime, feedback);
            offers.Clear();
            Dictionary<string, ServiceOfferDefinition> byId =
                configuredRuntime.Catalog.Offers
                    .Where(offer =>
                        offer.Kind == ServiceOfferKind.Workshop &&
                        string.Equals(
                            offer.LocationId,
                            LocationId,
                            StringComparison.Ordinal))
                    .ToDictionary(
                        offer => offer.OfferId,
                        StringComparer.Ordinal);
            for (int index = 0; index < BrochureOfferOrder.Length; index++)
            {
                if (byId.TryGetValue(
                        BrochureOfferOrder[index],
                        out ServiceOfferDefinition offer))
                {
                    offers.Add(offer);
                }
            }

            offers.AddRange(byId.Values
                .Where(offer => !BrochureOfferOrder.Contains(
                    offer.OfferId,
                    StringComparer.Ordinal))
                .OrderBy(offer => offer.OfferId, StringComparer.Ordinal));
            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, offers.Count - 1));
        }

        public void BindBookPresenter(
            IServiceCatalogBookPresenter configuredPresenter) =>
            bookPresenter = configuredPresenter ??
                throw new ArgumentNullException(nameof(configuredPresenter));

        public override void Interact(in InteractionContext context)
        {
            ServiceOfferDefinition offer = CurrentOffer;
            if (!CanInteract(context) || offer == null)
            {
                return;
            }

            bookPresenter?.TryOpen();
        }

        public ServiceCatalogBookEntry GetEntry(int index)
        {
            if (index < 0 || index >= offers.Count)
            {
                return default;
            }

            ServiceOfferDefinition offer = offers[index];
            return new ServiceCatalogBookEntry(
                offer.OfferId,
                offer.DisplayName,
                offer.BasePriceMinorUnits);
        }

        public bool IsEntrySelected(int index) =>
            index >= 0 && index < offers.Count &&
            IsSelected(offers[index].OfferId);

        public bool TryActivateEntry(int index, float variant = float.NaN)
        {
            if (Runtime == null || index < 0 || index >= offers.Count)
            {
                return false;
            }

            currentIndex = index;
            ServiceOfferDefinition offer = offers[index];
            WorkshopSelectionStateDto current =
                Runtime.CaptureWorkshopSelection();
            var ids = new List<string>(current.offerIds ?? Array.Empty<string>());
            bool wasSelected = ids.Remove(offer.OfferId);
            bool changingFinalGear = wasSelected &&
                                     string.Equals(
                                         offer.OfferId,
                                         "service.workshop.final-gear",
                                         StringComparison.Ordinal) &&
                                     float.IsFinite(variant) &&
                                     !Mathf.Approximately(
                                         current.finalGearRatio,
                                         variant);
            if (changingFinalGear)
            {
                ids.Add(offer.OfferId);
                wasSelected = false;
            }

            if (!wasSelected)
            {
                if (!string.IsNullOrEmpty(offer.ExclusiveGroupId))
                {
                    ids.RemoveAll(id =>
                        Runtime.Catalog.TryGetOffer(
                            id,
                            out ServiceOfferDefinition selected) &&
                        string.Equals(
                            selected.ExclusiveGroupId,
                            offer.ExclusiveGroupId,
                            StringComparison.Ordinal));
                }

                ids.Add(offer.OfferId);
            }

            var requested = new WorkshopSelectionStateDto
            {
                offerIds = ids.ToArray(),
                paintVariant = current.paintVariant,
                rimVariant = current.rimVariant,
                tireVariant = current.tireVariant,
                finalGearRatio = string.Equals(
                                     offer.OfferId,
                                     "service.workshop.final-gear",
                                     StringComparison.Ordinal) &&
                                 float.IsFinite(variant)
                    ? variant
                    : current.finalGearRatio,
            };
            ServiceResult result = Runtime.TrySetWorkshopSelection(requested);
            Publish(
                ServiceInteractionKind.WorkshopSelection,
                offer.OfferId,
                result,
                MessageFor(
                    result,
                    wasSelected
                        ? "Услуга убрана из заказа"
                        : "Услуга добавлена в заказ"));
            return result.Succeeded;
        }

        public bool TryConfirm()
        {
            if (Runtime == null)
            {
                return false;
            }

            ServiceResult result = Runtime.TryPlaceWorkshopOrder(
                Runtime.AllocateOperationId("fleetari-catalog"));
            Publish(
                ServiceInteractionKind.WorkshopOrder,
                string.Empty,
                result,
                MessageFor(result, "Заказ Флитари оформлен"));
            return result.Succeeded;
        }

        private ServiceOfferDefinition CurrentOffer =>
            currentIndex >= 0 && currentIndex < offers.Count
                ? offers[currentIndex]
                : null;

        private bool IsSelected(string offerId) =>
            Runtime != null &&
            (Runtime.CaptureWorkshopSelection().offerIds ??
             Array.Empty<string>()).Contains(
                offerId,
                StringComparer.Ordinal);

        private static string FormatMoney(long minorUnits) =>
            $"{minorUnits / 100L},{Math.Abs(minorUnits % 100L):00}";
    }

    /// <summary>
    /// Physical home parts catalog browser backed by the audited donor offer
    /// list. It intentionally owns only browsing/form presentation; mail-order
    /// payment and delivery remain separate service authority.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionTargetHost))]
    public sealed class HomePartsCatalogInteractionTarget : MonoBehaviour,
        IInteractionDisplayTarget,
        IToolActivationTarget,
        IServiceCatalogBookSelection
    {
        private HomePartsMailOrderOffer[] entries =
            Array.Empty<HomePartsMailOrderOffer>();
        private readonly HashSet<string> selected =
            new(StringComparer.Ordinal);
        private Action<string> feedback;
        private ServiceRuntime mailOrderRuntime;
        private Func<HomeMailOrderEnvelopePlan, bool> envelopeMaterializer;
        private IServiceCatalogBookPresenter bookPresenter;
        private int currentIndex;

        public int EntryCount => entries.Length;
        public int CurrentIndex => currentIndex;
        public int SelectedCount => mailOrderRuntime != null
            ? mailOrderRuntime.HomeMailOrderDraftCount
            : selected.Count;
        public string CurrentEntryId => CurrentEntry.Id ?? string.Empty;
        public long SelectedTotalMinorUnits => mailOrderRuntime != null
            ? mailOrderRuntime.HomeMailOrderDraftTotalMinorUnits
            : entries.Where(entry => selected.Contains(entry.Id))
                .Sum(entry => entry.PriceMinorUnits);

        public string InteractionDisplayName => entries.Length == 0
            ? "Каталог запчастей"
            : $"{currentIndex + 1}/{entries.Length}  " +
              $"{CurrentEntry.DisplayName} — " +
              $"{FormatMoney(CurrentEntry.PriceMinorUnits)} MK " +
              (IsSelected(CurrentEntry.Id) ? "[в бланке]" : string.Empty);

        public string InteractionPrompt => entries.Length == 0
            ? "Каталог пуст"
            : "Открыть каталог";

        public string ToolPrompt => "Открыть каталог";

        public bool CanActivateTool(in InteractionContext context) =>
            enabled && gameObject.activeInHierarchy && entries.Length > 0;

        public void ActivateTool(in InteractionContext context)
        {
            if (CanActivateTool(context))
            {
                bookPresenter?.TryOpen();
            }
        }

        public void ConfigureForAuthoring(
            IEnumerable<HomePartsMailOrderOffer> configuredEntries,
            Action<string> configuredFeedback = null)
        {
            entries = (configuredEntries ??
                    Enumerable.Empty<HomePartsMailOrderOffer>())
                .Where(entry =>
                    !string.IsNullOrWhiteSpace(entry.Id) &&
                    !string.IsNullOrWhiteSpace(entry.DisplayName) &&
                    entry.PriceMinorUnits > 0)
                .GroupBy(entry => entry.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            feedback = configuredFeedback;
            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, entries.Length - 1));
            GetComponent<InteractionTargetHost>().Configure(this);
        }

        public void BindBookPresenter(
            IServiceCatalogBookPresenter configuredPresenter) =>
            bookPresenter = configuredPresenter ??
                throw new ArgumentNullException(nameof(configuredPresenter));

        public void BindMailOrder(
            ServiceRuntime configuredRuntime,
            Func<HomeMailOrderEnvelopePlan, bool> configuredMaterializer)
        {
            mailOrderRuntime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
            envelopeMaterializer = configuredMaterializer ??
                throw new ArgumentNullException(nameof(configuredMaterializer));
            selected.Clear();
        }

        public ServiceCatalogBookEntry GetEntry(int index)
        {
            if (index < 0 || index >= entries.Length)
            {
                return default;
            }

            HomePartsMailOrderOffer entry = entries[index];
            return new ServiceCatalogBookEntry(
                entry.Id,
                entry.DisplayName,
                entry.PriceMinorUnits);
        }

        public bool IsEntrySelected(int index) =>
            index >= 0 && index < entries.Length &&
            IsSelected(entries[index].Id);

        public bool TryActivateEntry(int index, float variant = float.NaN)
        {
            if (index < 0 || index >= entries.Length)
            {
                return false;
            }

            currentIndex = index;
            HomePartsMailOrderOffer entry = CurrentEntry;
            bool wasSelected = IsSelected(entry.Id);
            bool changed;
            if (mailOrderRuntime != null)
            {
                changed = mailOrderRuntime.TryToggleHomeMailOrderDraft(entry.Id);
            }
            else
            {
                changed = wasSelected
                    ? selected.Remove(entry.Id)
                    : selected.Add(entry.Id);
            }

            bool added = changed && !wasSelected;

            feedback?.Invoke(
                added
                    ? $"{entry.DisplayName}: добавлено в бланк; сумма {FormatMoney(SelectedTotalMinorUnits)} MK"
                    : $"{entry.DisplayName}: убрано из бланка; сумма {FormatMoney(SelectedTotalMinorUnits)} MK");
            return true;
        }

        public bool TryConfirm()
        {
            if (SelectedCount == 0)
            {
                feedback?.Invoke("В бланке заказа ничего не отмечено");
                return false;
            }

            if (mailOrderRuntime == null || envelopeMaterializer == null)
            {
                feedback?.Invoke("Почтовая отправка пока недоступна");
                return false;
            }

            ServiceResult result = mailOrderRuntime.TryCreateHomeMailOrderEnvelope(
                envelopeMaterializer);
            feedback?.Invoke(result.Succeeded
                ? "Заказ записан в конверт — отнесите его в почтовый ящик у Теймо"
                : result.FailureReason == ServiceFailureReason.OrderAlreadyActive
                    ? "Сначала закончите предыдущий почтовый заказ"
                    : "Не удалось подготовить конверт заказа");
            return result.Succeeded;
        }

        private bool IsSelected(string offerId) =>
            mailOrderRuntime != null
                ? mailOrderRuntime.IsHomeMailOrderDraftSelected(offerId)
                : selected.Contains(offerId);

        private HomePartsMailOrderOffer CurrentEntry =>
            entries.Length == 0 ? default : entries[currentIndex];

        private static string FormatMoney(long minorUnits) =>
            $"{minorUnits / 100L},{Math.Abs(minorUnits % 100L):00}";

        private void Awake() =>
            GetComponent<InteractionTargetHost>().Configure(this);
    }
}
