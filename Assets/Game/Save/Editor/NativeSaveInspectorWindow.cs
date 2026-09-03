using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MSC.Save.EditorTools
{
    public sealed class NativeSaveInspectorWindow : EditorWindow
    {
        private const string RootEditorPreference = "MSC.NativeSaveInspector.Root";
        private const float SlotPaneWidth = 250f;
        private const int MaximumPayloadPreviewCharacters = 16 * 1024;

        private static UnresolvedContentEntry[] pendingUnresolvedEntries = Array.Empty<UnresolvedContentEntry>();

        private readonly HashSet<string> expandedDomains = new HashSet<string>(StringComparer.Ordinal);
        private string rootDirectory = string.Empty;
        private string selectedSlotId = string.Empty;
        private string lastFailure = string.Empty;
        private NativeSaveRootValidation rootValidation;
        private NativeSaveSlotInspection selectedInspection;
        private UnresolvedContentEntry[] transientUnresolvedEntries = Array.Empty<UnresolvedContentEntry>();
        private Vector2 slotScroll;
        private Vector2 detailScroll;

        [MenuItem("Tools/MSC Remake/Save/Native Save Inspector")]
        public static void Open()
        {
            GetWindow<NativeSaveInspectorWindow>("Native Saves");
        }

        /// <summary>
        /// Allows an Editor-only debug bridge to display the unresolved report returned by a real load.
        /// The report is transient and is never written into the save document by this tool.
        /// </summary>
        public static void OpenWithUnresolvedReport(UnresolvedContentReport report)
        {
            pendingUnresolvedEntries = report?.Snapshot() ?? Array.Empty<UnresolvedContentEntry>();
            NativeSaveInspectorWindow window = GetWindow<NativeSaveInspectorWindow>("Native Saves");
            window.transientUnresolvedEntries = pendingUnresolvedEntries
                .Select(entry => entry.DeepClone())
                .ToArray();
            window.Show();
            window.Repaint();
        }

        public static string DefaultSaveRoot => Path.Combine(Application.persistentDataPath, "Saves");

        private void OnEnable()
        {
            rootDirectory = EditorPrefs.GetString(RootEditorPreference, DefaultSaveRoot);
            transientUnresolvedEntries = pendingUnresolvedEntries
                .Select(entry => entry.DeepClone())
                .ToArray();
            Refresh();
        }

        private void OnGUI()
        {
            DrawRootToolbar();

            if (!string.IsNullOrWhiteSpace(lastFailure))
            {
                EditorGUILayout.HelpBox(lastFailure, MessageType.Error);
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSlotPane();
                DrawDetailPane();
            }
        }

        private void DrawRootToolbar()
        {
            EditorGUILayout.LabelField("Native save root", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                string updatedRoot = EditorGUILayout.TextField(rootDirectory);
                if (EditorGUI.EndChangeCheck())
                {
                    rootDirectory = updatedRoot;
                    EditorPrefs.SetString(RootEditorPreference, rootDirectory);
                }

                if (GUILayout.Button("Browse...", GUILayout.Width(80f)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Select native save root", rootDirectory, string.Empty);
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        rootDirectory = selected;
                        EditorPrefs.SetString(RootEditorPreference, rootDirectory);
                        Refresh();
                    }
                }

                if (GUILayout.Button("Default", GUILayout.Width(70f)))
                {
                    rootDirectory = DefaultSaveRoot;
                    EditorPrefs.SetString(RootEditorPreference, rootDirectory);
                    Refresh();
                }

                if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
                {
                    Refresh();
                }

                using (new EditorGUI.DisabledScope(!Directory.Exists(rootDirectory)))
                {
                    if (GUILayout.Button("Reveal", GUILayout.Width(60f)))
                    {
                        EditorUtility.RevealInFinder(rootDirectory);
                    }
                }
            }

            if (rootValidation != null)
            {
                MessageType type = rootValidation.IsValid ? MessageType.Info : MessageType.Warning;
                string status = rootValidation.IsValid
                    ? $"Validated {rootValidation.Slots.Count} slot director{(rootValidation.Slots.Count == 1 ? "y" : "ies")}; no corrupt active slot was found."
                    : $"Validation found {rootValidation.Issues.Count} issue(s). Recovery is never automatic in this window.";
                EditorGUILayout.HelpBox(status, type);
            }
        }

        private void DrawSlotPane()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(SlotPaneWidth)))
            {
                EditorGUILayout.LabelField("Slots", EditorStyles.boldLabel);
                slotScroll = EditorGUILayout.BeginScrollView(slotScroll, GUI.skin.box);
                if (rootValidation == null || rootValidation.Slots.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        Directory.Exists(rootDirectory)
                            ? "No slot directories found."
                            : "Save root does not exist yet.",
                        MessageType.Info);
                }
                else
                {
                    foreach (NativeSaveSlotInspection inspection in rootValidation.Slots)
                    {
                        bool selected = string.Equals(selectedSlotId, inspection.SlotId, StringComparison.Ordinal);
                        GUIStyle style = selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                        string label = $"{StateGlyph(inspection.State)}  {inspection.SlotId}\n{inspection.State}";
                        if (GUILayout.Button(label, style, GUILayout.Height(42f)))
                        {
                            selectedSlotId = inspection.SlotId;
                            selectedInspection = inspection;
                            detailScroll = Vector2.zero;
                        }
                    }
                }

                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("Validate all slots"))
                {
                    LogValidation(rootValidation);
                }
            }
        }

        private void DrawDetailPane()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                if (selectedInspection == null)
                {
                    EditorGUILayout.HelpBox("Select a save slot to inspect its candidates and document.", MessageType.Info);
                    DrawTransientUnresolvedEntries();
                    EditorGUILayout.EndScrollView();
                    return;
                }

                EditorGUILayout.LabelField($"Slot: {selectedInspection.SlotId}", EditorStyles.largeLabel);
                EditorGUILayout.HelpBox(selectedInspection.Message, MessageForState(selectedInspection.State));
                if (!string.IsNullOrWhiteSpace(selectedInspection.SlotDirectory))
                {
                    EditorGUILayout.LabelField("Directory", selectedInspection.SlotDirectory);
                }

                DrawRecoveryControls();
                DrawCandidates();
                DrawDocument(selectedInspection.PreferredDocument);
                DrawStableEntityReferences();
                DrawTransientUnresolvedEntries();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRecoveryControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!selectedInspection.CanRecover))
                {
                    if (GUILayout.Button("Recover validated candidate", GUILayout.Width(220f)))
                    {
                        bool confirmed = EditorUtility.DisplayDialog(
                            "Recover native save",
                            "Recovery can quarantine an invalid current file and promote a validated interrupted write or backup. Continue?",
                            "Recover",
                            "Cancel");
                        if (confirmed)
                        {
                            RecoverSelectedSlot();
                        }
                    }
                }

                if (GUILayout.Button("Copy slot path", GUILayout.Width(120f)) &&
                    !string.IsNullOrWhiteSpace(selectedInspection.SlotDirectory))
                {
                    EditorGUIUtility.systemCopyBuffer = selectedInspection.SlotDirectory;
                }
            }
        }

        private void DrawCandidates()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Storage candidates / recovery evidence", EditorStyles.boldLabel);
            if (selectedInspection.Candidates.Count == 0)
            {
                EditorGUILayout.LabelField("No current, temporary, backup, or quarantined files.");
                return;
            }

            foreach (SaveCandidateInspection candidate in selectedInspection.Candidates)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        $"{candidate.Kind}: {(candidate.IsValid ? "VALID" : "INVALID")}",
                        candidate.IsValid ? EditorStyles.boldLabel : EditorStyles.label);
                    EditorGUILayout.SelectableLabel(candidate.Path, EditorStyles.textField, GUILayout.Height(18f));
                    if (!candidate.IsValid)
                    {
                        EditorGUILayout.HelpBox(candidate.Failure, MessageType.Warning);
                    }
                    else if (candidate.Document?.Header != null)
                    {
                        EditorGUILayout.LabelField(
                            "Document",
                            $"v{candidate.Document.Header.DocumentVersion}, updated {candidate.Document.Header.UpdatedUtc}");
                    }
                }
            }
        }

        private void DrawDocument(SaveDocument document)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Preferred validated document", EditorStyles.boldLabel);
            if (document == null)
            {
                EditorGUILayout.HelpBox("No validated document is available for inspection.", MessageType.Warning);
                return;
            }

            SaveHeader header = document.Header;
            SaveMetadata metadata = document.Metadata;
            EditorGUILayout.LabelField("Source", selectedInspection.PreferredCandidate?.ToString() ?? "Unknown");
            EditorGUILayout.LabelField("Format / version", $"{header.FormatId} / {header.DocumentVersion}");
            EditorGUILayout.LabelField("Save ID", header.SaveId);
            EditorGUILayout.LabelField("Build ID", header.BuildId);
            EditorGUILayout.LabelField("Created UTC", header.CreatedUtc);
            EditorGUILayout.LabelField("Updated UTC", header.UpdatedUtc);
            EditorGUILayout.LabelField("Integrity SHA-256", header.IntegritySha256);
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Display name", metadata.DisplayName);
            EditorGUILayout.LabelField("Play time", $"{metadata.PlayTimeSeconds:0.###} s");
            EditorGUILayout.LabelField("Game timestamp", metadata.GameTimestamp);
            EditorGUILayout.LabelField("Location stable ID", metadata.LocationStableId);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Domains ({document.Domains?.Length ?? 0})", EditorStyles.boldLabel);
                if (GUILayout.Button("Copy canonical document JSON", GUILayout.Width(210f)))
                {
                    EditorGUIUtility.systemCopyBuffer = new SaveDocumentCodec().Serialize(document, true);
                }
            }

            foreach (SaveDomainEnvelope domain in document.Domains ?? Array.Empty<SaveDomainEnvelope>())
            {
                if (domain == null)
                {
                    continue;
                }

                bool expanded = expandedDomains.Contains(domain.DomainId);
                bool nextExpanded = EditorGUILayout.Foldout(
                    expanded,
                    $"{domain.DomainId}  | schema {domain.SchemaVersion} | {(domain.Required ? "required" : "optional")}",
                    true);
                if (nextExpanded != expanded)
                {
                    if (nextExpanded)
                    {
                        expandedDomains.Add(domain.DomainId);
                    }
                    else
                    {
                        expandedDomains.Remove(domain.DomainId);
                    }
                }

                if (!nextExpanded)
                {
                    continue;
                }

                string payload = domain.PayloadJson ?? string.Empty;
                string preview = payload.Length <= MaximumPayloadPreviewCharacters
                    ? payload
                    : payload.Substring(0, MaximumPayloadPreviewCharacters) + "\n... preview truncated ...";
                EditorGUILayout.TextArea(preview, GUILayout.MinHeight(60f), GUILayout.MaxHeight(180f));
                if (GUILayout.Button("Copy full domain payload", GUILayout.Width(180f)))
                {
                    EditorGUIUtility.systemCopyBuffer = payload;
                }
            }
        }

        private void DrawStableEntityReferences()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Stable-ID / deferred / unresolved payload view", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The inspector reports project-owned stable-ID fields found in domain payloads. " +
                "Only paths explicitly named Deferred or Unresolved are classified as such; ordinary references are not claimed missing.",
                MessageType.Info);

            foreach (DomainPayloadInspectionIssue issue in selectedInspection.PayloadIssues)
            {
                EditorGUILayout.HelpBox($"{issue.DomainId}: {issue.Message}", MessageType.Warning);
            }

            if (selectedInspection.StableEntityReferences.Count == 0)
            {
                EditorGUILayout.LabelField("No stable-ID fields found in the preferred document.");
                return;
            }

            foreach (StableEntityReferenceInspection item in selectedInspection.StableEntityReferences)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(item.Kind.ToString(), GUILayout.Width(78f));
                    EditorGUILayout.LabelField(item.DomainId, GUILayout.Width(150f));
                    EditorGUILayout.SelectableLabel(item.StableEntityId, EditorStyles.textField, GUILayout.Height(18f));
                    EditorGUILayout.LabelField(item.FieldPath, GUILayout.MinWidth(160f));
                }
            }
        }

        private void DrawTransientUnresolvedEntries()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Last runtime unresolved report (transient)", EditorStyles.boldLabel);
            if (transientUnresolvedEntries.Length == 0)
            {
                EditorGUILayout.LabelField(
                    "No report was supplied. A real SaveLoadResult.UnresolvedContent report can be opened through OpenWithUnresolvedReport().");
                return;
            }

            foreach (UnresolvedContentEntry entry in transientUnresolvedEntries)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{entry.Reason} — {entry.DomainId}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Stable entity", entry.StableEntityId);
                    EditorGUILayout.LabelField("Detail", entry.Detail, EditorStyles.wordWrappedLabel);
                }
            }

            if (GUILayout.Button("Clear transient report", GUILayout.Width(160f)))
            {
                transientUnresolvedEntries = Array.Empty<UnresolvedContentEntry>();
                pendingUnresolvedEntries = Array.Empty<UnresolvedContentEntry>();
            }
        }

        private void RecoverSelectedSlot()
        {
            try
            {
                NativeSaveInspectionService service = new NativeSaveInspectionService(rootDirectory);
                SaveReadResult result = service.RecoverSlot(selectedSlotId);
                if (!result.IsSuccess)
                {
                    throw new InvalidDataException(result.Message);
                }

                ShowNotification(new GUIContent($"{selectedSlotId}: {result.Status}"));
                Refresh();
            }
            catch (Exception exception) when (IsExpectedEditorFailure(exception))
            {
                lastFailure = exception.Message;
                Debug.LogException(exception);
            }
        }

        private void Refresh()
        {
            lastFailure = string.Empty;
            rootValidation = null;
            selectedInspection = null;
            try
            {
                NativeSaveInspectionService service = new NativeSaveInspectionService(rootDirectory);
                rootValidation = service.ValidateRoot();
                if (!string.IsNullOrWhiteSpace(selectedSlotId))
                {
                    selectedInspection = rootValidation.Slots.FirstOrDefault(
                        slot => string.Equals(slot.SlotId, selectedSlotId, StringComparison.Ordinal));
                }

                if (selectedInspection == null && rootValidation.Slots.Count > 0)
                {
                    selectedInspection = rootValidation.Slots[0];
                    selectedSlotId = selectedInspection.SlotId;
                }
            }
            catch (Exception exception) when (IsExpectedEditorFailure(exception))
            {
                lastFailure = exception.Message;
            }

            Repaint();
        }

        private static void LogValidation(NativeSaveRootValidation validation)
        {
            if (validation == null)
            {
                Debug.LogWarning("Native save root has not been inspected.");
                return;
            }

            if (validation.IsValid)
            {
                Debug.Log($"Native save validation passed for {validation.Slots.Count} slot director{(validation.Slots.Count == 1 ? "y" : "ies")}.");
                return;
            }

            Debug.LogError("Native save validation failed:\n" + string.Join("\n", validation.Issues));
        }

        private static MessageType MessageForState(NativeSaveSlotState state)
        {
            return state == NativeSaveSlotState.Valid || state == NativeSaveSlotState.Empty
                ? MessageType.Info
                : state == NativeSaveSlotState.Recoverable
                    ? MessageType.Warning
                    : MessageType.Error;
        }

        private static string StateGlyph(NativeSaveSlotState state)
        {
            return state == NativeSaveSlotState.Valid ? "✓" :
                state == NativeSaveSlotState.Recoverable ? "↺" :
                state == NativeSaveSlotState.Empty ? "○" : "!";
        }

        private static bool IsExpectedEditorFailure(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is FormatException ||
                   exception is InvalidDataException ||
                   exception is NotSupportedException ||
                   exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is OverflowException;
        }
    }

    public static class NativeSaveEditorCommands
    {
        [MenuItem("Tools/MSC Remake/Save/Validate All Native Save Slots")]
        public static void ValidateDefaultRoot()
        {
            try
            {
                NativeSaveRootValidation validation = new NativeSaveInspectionService(
                    NativeSaveInspectorWindow.DefaultSaveRoot).ValidateRoot();
                if (validation.IsValid)
                {
                    Debug.Log($"Native save validation passed for {validation.Slots.Count} slot director{(validation.Slots.Count == 1 ? "y" : "ies")}.");
                    return;
                }

                Debug.LogError("Native save validation failed:\n" + string.Join("\n", validation.Issues));
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidDataException ||
                exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                Debug.LogException(exception);
            }
        }
    }
}
