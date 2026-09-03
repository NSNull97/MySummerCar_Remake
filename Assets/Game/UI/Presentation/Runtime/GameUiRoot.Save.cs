using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MSC.Save;
using MSC.UI.Runtime.Routing;
using UnityEngine;
using UnityEngine.UI;

namespace MSC.UI.Presentation
{
    public sealed partial class GameUiRoot
    {
        private static readonly string[] ManualSaveSlotIds =
        {
            "slot-01",
            "slot-02",
            "slot-03",
        };

        private readonly List<SaveSlotSummary> saveSlotSummaries =
            new List<SaveSlotSummary>();

        private Button mainContinueButton;
        private Button mainLoadButton;
        private Button saveStatusPreviousButton;
        private Button saveStatusNextButton;
        private Button saveStatusSaveButton;
        private Button saveStatusLoadButton;
        private Text saveStatusStateText;
        private Text saveStatusDetailText;
        private Text saveStatusOperationText;
        private Text loadingDetailText;
        private Coroutine saveLoadRequestCoroutine;
        private string selectedSaveSlotId = string.Empty;
        private string saveOperationStatus = string.Empty;
        private bool saveServiceBound;
        private string initialSaveNoticeKey = string.Empty;

        private SaveSlotSummary LatestValidSaveSlot => saveSlotSummaries
            .Where(slot => slot != null && slot.IsValid)
            .OrderByDescending(slot => ParseUpdatedUtc(slot.UpdatedUtc))
            .ThenBy(slot => slot.SlotId, StringComparer.Ordinal)
            .FirstOrDefault();

        private SaveSlotSummary SelectedSaveSlot
        {
            get
            {
                SaveSlotSummary selected = saveSlotSummaries.FirstOrDefault(
                    slot => string.Equals(
                        slot.SlotId,
                        selectedSaveSlotId,
                        StringComparison.Ordinal));
                return selected;
            }
        }

        private bool CanRequestLatestLoad =>
            dependencies?.RequestLoad != null &&
            LatestValidSaveSlot != null &&
            dependencies.SaveService?.IsOperationInProgress != true;

        private bool CanOpenLoadGame =>
            dependencies?.SaveService != null &&
            dependencies.SaveService.IsOperationInProgress == false;

        private bool CanRequestSelectedLoad =>
            dependencies?.RequestLoad != null &&
            SelectedSaveSlot?.IsValid == true &&
            dependencies.SaveService?.IsOperationInProgress != true;

        private bool CanWriteSelectedSlot =>
            gameplayStarted &&
            saveStatusReturnRoute == UiRouteId.Pause &&
            dependencies?.SaveService != null &&
            dependencies.CreateSaveRequest != null &&
            !dependencies.SaveService.IsOperationInProgress;

        private void InitializeSaveUi()
        {
            RefreshSaveSlotsFromStorage();
            if (!string.IsNullOrWhiteSpace(dependencies?.InitialSaveStatus))
            {
                saveOperationStatus =
                    textCatalog.Get("ui.save_status.failed") + " " +
                    dependencies.InitialSaveStatus;
                initialSaveNoticeKey = "ui.notice.load_failed";
            }
            else if (dependencies?.InitialSaveReadStatus ==
                     SaveReadStatus.RecoveredFromInterruptedWrite)
            {
                saveOperationStatus = textCatalog.Get(
                    "ui.save_status.recovered_interrupted");
                initialSaveNoticeKey =
                    "ui.notice.save_recovered_interrupted";
            }
            else if (dependencies?.InitialSaveReadStatus ==
                     SaveReadStatus.RecoveredFromBackup)
            {
                saveOperationStatus = textCatalog.Get(
                    "ui.save_status.recovered_backup");
                initialSaveNoticeKey =
                    "ui.notice.save_recovered_backup";
            }
            BindSaveService();
        }

        private void PresentInitialSaveNotice()
        {
            if (string.IsNullOrEmpty(initialSaveNoticeKey))
            {
                return;
            }

            ShowNotice(initialSaveNoticeKey);
            initialSaveNoticeKey = string.Empty;
        }

        private void BindSaveService()
        {
            ISaveService service = dependencies?.SaveService;
            if (service == null || saveServiceBound)
            {
                return;
            }

            service.OperationStarted += OnSaveOperationStarted;
            service.OperationCompleted += OnSaveOperationCompleted;
            service.OperationFailed += OnSaveOperationFailed;
            service.RecoveryPerformed += OnSaveRecoveryPerformed;
            saveServiceBound = true;
        }

        private void UnbindSaveService()
        {
            ISaveService service = dependencies?.SaveService;
            if (service == null || !saveServiceBound)
            {
                return;
            }

            service.OperationStarted -= OnSaveOperationStarted;
            service.OperationCompleted -= OnSaveOperationCompleted;
            service.OperationFailed -= OnSaveOperationFailed;
            service.RecoveryPerformed -= OnSaveRecoveryPerformed;
            saveServiceBound = false;
        }

        private void OnSaveOperationStarted(object sender, SaveOperationEventArgs args)
        {
            saveOperationStatus = args.Operation == SaveOperationKind.Save
                ? textCatalog.Get("ui.save_status.saving")
                : args.Operation == SaveOperationKind.Load
                    ? textCatalog.Get("ui.save_status.loading")
                    : textCatalog.Get("ui.save_status.refreshing");
            RefreshSaveUiPresentation();
        }

        private void OnSaveOperationCompleted(object sender, SaveOperationEventArgs args)
        {
            saveOperationStatus = args.Operation == SaveOperationKind.Save
                ? textCatalog.Get("ui.save_status.saved")
                : args.Operation == SaveOperationKind.Load
                    ? textCatalog.Get("ui.save_status.loaded")
                    : string.Empty;
            RefreshSaveUiPresentation();
        }

        private void OnSaveOperationFailed(object sender, SaveOperationEventArgs args)
        {
            saveOperationStatus = textCatalog.Get("ui.save_status.failed") + " " + args.Message;
            RefreshSaveUiPresentation();
            ShowNotice("ui.notice.save_failed");
        }

        private void OnSaveRecoveryPerformed(object sender, SaveOperationEventArgs args)
        {
            saveOperationStatus = textCatalog.Get("ui.save_status.recovered");
            RefreshSaveUiPresentation();
            ShowNotice("ui.notice.save_recovered");
        }

        private void RefreshSaveSlotsFromStorage()
        {
            saveSlotSummaries.Clear();
            ISaveService service = dependencies?.SaveService;
            if (service == null)
            {
                saveOperationStatus = textCatalog == null
                    ? string.Empty
                    : textCatalog.Get("ui.save_status.no_provider");
                return;
            }

            try
            {
                IReadOnlyList<SaveSlotSummary> slots = service.EnumerateSlots();
                if (slots != null)
                {
                    saveSlotSummaries.AddRange(slots.Where(slot => slot != null));
                }

                EnsureSelectedSaveSlot();
            }
            catch (Exception exception)
            {
                saveOperationStatus = textCatalog.Get("ui.save_status.failed") + " " + exception.Message;
                Debug.LogError("M09A save-slot enumeration failed: " + exception.Message, this);
            }
        }

        private void EnsureSelectedSaveSlot()
        {
            IReadOnlyList<string> selectableSlotIds =
                GetSelectableSaveSlotIds();
            if (selectableSlotIds.Contains(
                    selectedSaveSlotId,
                    StringComparer.Ordinal))
            {
                return;
            }

            SaveSlotSummary preferred = saveSlotSummaries.FirstOrDefault(
                slot => string.Equals(
                    slot.SlotId,
                    dependencies.PreferredSaveSlotId,
                    StringComparison.Ordinal));
            selectedSaveSlotId =
                preferred?.SlotId ??
                LatestValidSaveSlot?.SlotId ??
                saveSlotSummaries.FirstOrDefault()?.SlotId ??
                dependencies.PreferredSaveSlotId ??
                ManualSaveSlotIds[0];
        }

        private IReadOnlyList<string> GetSelectableSaveSlotIds()
        {
            var slotIds = new List<string>(
                ManualSaveSlotIds.Length + saveSlotSummaries.Count);
            slotIds.AddRange(ManualSaveSlotIds);
            for (int index = 0; index < saveSlotSummaries.Count; index++)
            {
                string slotId = saveSlotSummaries[index]?.SlotId;
                if (!string.IsNullOrWhiteSpace(slotId) &&
                    !slotIds.Contains(slotId, StringComparer.Ordinal))
                {
                    slotIds.Add(slotId);
                }
            }

            return slotIds;
        }

        private string MainSaveHelperText()
        {
            if (dependencies?.SaveService == null)
            {
                return textCatalog.Get("ui.main.no_save");
            }

            SaveSlotSummary latest = LatestValidSaveSlot;
            if (latest == null)
            {
                return textCatalog.Get("ui.main.no_valid_save");
            }

            string displayName = string.IsNullOrWhiteSpace(latest.Metadata?.DisplayName)
                ? latest.SlotId
                : latest.Metadata.DisplayName;
            return textCatalog.Get("ui.main.latest_save") + " " + displayName;
        }

        private void BeginContinueLoad()
        {
            SaveSlotSummary latest = LatestValidSaveSlot;
            if (latest != null)
            {
                BeginCleanSaveLoad(latest.SlotId, UiRouteId.MainMenu);
            }
        }

        private void OpenLoadGame()
        {
            RefreshSaveSlotsFromStorage();
            OpenSaveStatus(UiRouteId.MainMenu);
        }

        private void BeginSelectedSaveLoad()
        {
            SaveSlotSummary selected = SelectedSaveSlot;
            if (selected != null && selected.IsValid)
            {
                BeginCleanSaveLoad(selected.SlotId, UiRouteId.SaveStatus);
            }
        }

        private void BeginCleanSaveLoad(string slotId, UiRouteId failureReturnRoute)
        {
            if (saveLoadRequestCoroutine != null || dependencies.RequestLoad == null)
            {
                return;
            }

            saveLoadRequestCoroutine = StartCoroutine(
                RequestCleanSaveLoadAfterLoadingFrame(slotId, failureReturnRoute));
        }

        private IEnumerator RequestCleanSaveLoadAfterLoadingFrame(
            string slotId,
            UiRouteId failureReturnRoute)
        {
            SetGameplaySuspended(true);
            if (loadingDetailText != null)
            {
                loadingDetailText.text = textCatalog.Get("ui.loading.save_detail");
            }

            ShowRoute(UiRouteId.Loading);
            yield return null;

            bool accepted = false;
            string failure = string.Empty;
            try
            {
                accepted = dependencies.RequestLoad(slotId, out failure);
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }

            if (!accepted && !sessionEnded)
            {
                saveOperationStatus = textCatalog.Get("ui.save_status.failed") + " " + failure;
                ShowRoute(failureReturnRoute);
                ShowNotice("ui.notice.load_failed");
                RefreshSaveUiPresentation();
            }

            // A successful request hands ownership to the composition root. It
            // reloads Bootstrap and consumes the pending slot before world reveal.
            saveLoadRequestCoroutine = null;
        }

        private void SaveSelectedSlot()
        {
            if (!CanWriteSelectedSlot)
            {
                return;
            }

            string slotId = string.IsNullOrWhiteSpace(selectedSaveSlotId)
                ? dependencies.PreferredSaveSlotId
                : selectedSaveSlotId;
            try
            {
                SaveRequest request = dependencies.CreateSaveRequest(slotId);
                if (request == null)
                {
                    throw new InvalidOperationException("The save-request factory returned null.");
                }

                if (string.IsNullOrWhiteSpace(request.SlotId))
                {
                    request.SlotId = slotId;
                }

                SaveWriteResult result = dependencies.SaveService.Save(request);
                selectedSaveSlotId = result.SlotId;
                RefreshSaveSlotsFromStorage();
                saveOperationStatus = textCatalog.Get("ui.save_status.saved");
                RefreshSaveUiPresentation();
                ShowNotice("ui.notice.game_saved");
            }
            catch (Exception exception)
            {
                saveOperationStatus = textCatalog.Get("ui.save_status.failed") + " " + exception.Message;
                RefreshSaveUiPresentation();
                ShowNotice("ui.notice.save_failed");
                Debug.LogError("M09A manual save failed: " + exception.Message, this);
            }
        }

        private void SelectRelativeSaveSlot(int offset)
        {
            IReadOnlyList<string> slotIds = GetSelectableSaveSlotIds();
            int current = -1;
            for (int index = 0; index < slotIds.Count; index++)
            {
                if (string.Equals(
                        slotIds[index],
                        selectedSaveSlotId,
                        StringComparison.Ordinal))
                {
                    current = index;
                    break;
                }
            }

            current = current < 0 ? 0 : current;
            int next = (current + offset) % slotIds.Count;
            if (next < 0)
            {
                next += slotIds.Count;
            }

            selectedSaveSlotId = slotIds[next];
            saveOperationStatus = string.Empty;
            RefreshSaveUiPresentation();
        }

        private bool HasMultipleSelectableSaveSlots() =>
            GetSelectableSaveSlotIds().Count > 1;

        private void RefreshSaveUiPresentation()
        {
            UpdateMainSaveButton(mainContinueButton, CanRequestLatestLoad, MainSaveHelperText());
            UpdateMainSaveButton(mainLoadButton, CanOpenLoadGame, MainSaveHelperText());

            SaveSlotSummary selected = SelectedSaveSlot;
            if (saveStatusStateText != null)
            {
                saveStatusStateText.text = SaveStatusTitle(selected);
            }

            if (saveStatusDetailText != null)
            {
                saveStatusDetailText.text = SaveStatusDetail(selected);
            }

            if (saveStatusOperationText != null)
            {
                saveStatusOperationText.text = saveOperationStatus ?? string.Empty;
            }

            bool hasMultipleSlots =
                HasMultipleSelectableSaveSlots() &&
                dependencies?.SaveService?.IsOperationInProgress != true;
            if (saveStatusPreviousButton != null)
            {
                saveStatusPreviousButton.interactable = hasMultipleSlots;
            }

            if (saveStatusNextButton != null)
            {
                saveStatusNextButton.interactable = hasMultipleSlots;
            }

            if (saveStatusSaveButton != null)
            {
                saveStatusSaveButton.interactable = CanWriteSelectedSlot;
            }

            if (saveStatusLoadButton != null)
            {
                saveStatusLoadButton.interactable = CanRequestSelectedLoad;
            }
        }

        private static void UpdateMainSaveButton(Button button, bool interactable, string helper)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            Text[] labels = button.GetComponentsInChildren<Text>(includeInactive: true);
            for (int index = 0; index < labels.Length; index++)
            {
                Text label = labels[index];
                bool isHelper = label.name.EndsWith("Helper", StringComparison.Ordinal);
                if (isHelper)
                {
                    label.text = helper ?? string.Empty;
                }

                label.color = interactable
                    ? isHelper ? UiThemeTokens.TextMuted : UiThemeTokens.TextPrimary
                    : UiThemeTokens.Disabled;
            }

            Image icon = button.transform.Find(button.name + "Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.color = interactable ? UiThemeTokens.TextPrimary : UiThemeTokens.Disabled;
            }
        }

        private string SaveStatusTitle(SaveSlotSummary selected)
        {
            if (dependencies?.SaveService == null)
            {
                return textCatalog.Get("ui.save_status.no_provider");
            }

            if (selected == null)
            {
                return selectedSaveSlotId + " — " +
                       textCatalog.Get("ui.save_status.empty");
            }

            string displayName = string.IsNullOrWhiteSpace(selected.Metadata?.DisplayName)
                ? selected.SlotId
                : selected.Metadata.DisplayName;
            return selected.IsValid
                ? displayName
                : displayName + " — " + textCatalog.Get("ui.save_status.invalid");
        }

        private string SaveStatusDetail(SaveSlotSummary selected)
        {
            if (dependencies?.SaveService == null)
            {
                return textCatalog.Get("ui.save_status.provider_detail");
            }

            if (selected == null)
            {
                return textCatalog.Get("ui.save_status.slot") + ": " +
                       selectedSaveSlotId + "\n" +
                       (saveStatusReturnRoute == UiRouteId.Pause
                           ? textCatalog.Get(
                               "ui.save_status.empty_pause_detail")
                           : textCatalog.Get(
                               "ui.save_status.empty_detail"));
            }

            if (!selected.IsValid)
            {
                return string.IsNullOrWhiteSpace(selected.Message)
                    ? textCatalog.Get("ui.save_status.invalid_detail")
                    : selected.Message;
            }

            SaveMetadata metadata = selected.Metadata;
            string updated = FormatUpdatedUtc(selected.UpdatedUtc);
            string gameTime = string.IsNullOrWhiteSpace(metadata?.GameTimestamp)
                ? textCatalog.Get("ui.common.not_available_short")
                : metadata.GameTimestamp;
            string location = string.IsNullOrWhiteSpace(metadata?.LocationStableId)
                ? textCatalog.Get("ui.common.not_available_short")
                : metadata.LocationStableId;
            return textCatalog.Get("ui.save_status.slot") + ": " + selected.SlotId + "\n" +
                   textCatalog.Get("ui.save_status.updated") + ": " + updated + "  •  " +
                   textCatalog.Get("ui.save_status.game_time") + ": " + gameTime + "\n" +
                   textCatalog.Get("ui.save_status.location") + ": " + location;
        }

        private string FormatUpdatedUtc(string value)
        {
            if (!DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset timestamp))
            {
                return textCatalog.Get("ui.common.not_available_short");
            }

            return timestamp.ToLocalTime().ToString("g", GetLocaleFormatter().Culture);
        }

        private static DateTimeOffset ParseUpdatedUtc(string value)
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset result)
                ? result
                : DateTimeOffset.MinValue;
        }

        private void BuildNativeSaveStatusRoute()
        {
            GameObject route = CreateRoute(UiRouteId.SaveStatus);
            GameObject panel = factory.Panel(
                "SaveStatusCard",
                route.transform,
                536f,
                252f,
                600f,
                436f);
            factory.Heading(
                panel.transform,
                textCatalog.Get("ui.save_status.title"),
                34f,
                28f,
                532f,
                27);
            saveStatusStateText = factory.Text(
                "SaveStatusState",
                panel.transform,
                string.Empty,
                34f,
                78f,
                532f,
                42f,
                18,
                UiThemeTokens.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            saveStatusDetailText = factory.Text(
                "SaveStatusDetail",
                panel.transform,
                string.Empty,
                34f,
                126f,
                532f,
                82f,
                14,
                UiThemeTokens.TextMuted,
                TextAnchor.UpperLeft);
            saveStatusOperationText = factory.Text(
                "SaveStatusOperation",
                panel.transform,
                string.Empty,
                96f,
                216f,
                408f,
                32f,
                11,
                UiThemeTokens.Accent,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            saveStatusPreviousButton = factory.CompactButton(
                "SaveStatusPrevious",
                panel.transform,
                "‹",
                34f,
                214f,
                48f,
                38f,
                () => SelectRelativeSaveSlot(-1));
            saveStatusNextButton = factory.CompactButton(
                "SaveStatusNext",
                panel.transform,
                "›",
                518f,
                214f,
                48f,
                38f,
                () => SelectRelativeSaveSlot(1));
            saveStatusSaveButton = factory.CompactButton(
                "SaveStatusSave",
                panel.transform,
                textCatalog.Get("ui.save_status.save"),
                34f,
                270f,
                252f,
                54f,
                SaveSelectedSlot,
                primary: true);
            saveStatusLoadButton = factory.CompactButton(
                "SaveStatusLoad",
                panel.transform,
                textCatalog.Get("ui.save_status.load"),
                314f,
                270f,
                252f,
                54f,
                BeginSelectedSaveLoad);
            factory.CompactButton(
                "SaveStatusBack",
                panel.transform,
                textCatalog.Get("ui.common.back"),
                174f,
                346f,
                252f,
                54f,
                ReturnFromSaveStatus);
            RefreshSaveUiPresentation();
        }
    }
}
