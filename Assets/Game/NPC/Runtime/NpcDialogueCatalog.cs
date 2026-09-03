using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Characters;
using UnityEngine;

namespace MSC.NPC
{
    public enum NpcDialogueConditionKind
    {
        CharacterFlag = 0,
        RelationshipAtLeast = 1,
        ExternalFlag = 2,
        ActiveScheduleBlock = 3,
    }

    [Serializable]
    public sealed class NpcDialogueCondition
    {
        [SerializeField] private NpcDialogueConditionKind kind;
        [SerializeField] private string conditionId = string.Empty;
        [SerializeField] private int threshold;
        [SerializeField] private bool expectedValue = true;

        public NpcDialogueCondition(
            NpcDialogueConditionKind configuredKind,
            string id,
            int configuredThreshold = 0,
            bool configuredExpectedValue = true)
        {
            kind = configuredKind;
            conditionId = id ?? string.Empty;
            threshold = configuredThreshold;
            expectedValue = configuredExpectedValue;
        }

        public NpcDialogueConditionKind Kind => kind;
        public string ConditionId => conditionId;
        public int Threshold => threshold;
        public bool ExpectedValue => expectedValue;
    }

    [Serializable]
    public sealed class NpcDialogueLine
    {
        [SerializeField] private string lineId = string.Empty;
        [SerializeField] private string localizationKey = string.Empty;
        [SerializeField] private string fallbackSubtitle = string.Empty;
        [SerializeField] private string audioEventId = string.Empty;
        [SerializeField, Min(0f)] private double cooldownGameSeconds;
        [SerializeField] private NpcDialogueCondition[] conditions =
            Array.Empty<NpcDialogueCondition>();
        [SerializeField] private string emittedEventId = string.Empty;

        public NpcDialogueLine(
            string id,
            string configuredLocalizationKey,
            string configuredAudioEventId,
            IEnumerable<NpcDialogueCondition> configuredConditions,
            string configuredEmittedEventId)
            : this(
                id,
                configuredLocalizationKey,
                configuredLocalizationKey,
                configuredAudioEventId,
                0d,
                configuredConditions,
                configuredEmittedEventId)
        {
        }

        public NpcDialogueLine(
            string id,
            string configuredLocalizationKey,
            string configuredFallbackSubtitle,
            string configuredAudioEventId,
            double configuredCooldownGameSeconds,
            IEnumerable<NpcDialogueCondition> configuredConditions,
            string configuredEmittedEventId)
        {
            lineId = id ?? string.Empty;
            localizationKey = configuredLocalizationKey ?? string.Empty;
            fallbackSubtitle = configuredFallbackSubtitle ?? string.Empty;
            audioEventId = configuredAudioEventId ?? string.Empty;
            cooldownGameSeconds = configuredCooldownGameSeconds;
            conditions = (configuredConditions ??
                Enumerable.Empty<NpcDialogueCondition>()).ToArray();
            emittedEventId = configuredEmittedEventId ?? string.Empty;
        }

        public string LineId => lineId;
        public string LocalizationKey => localizationKey;
        public string FallbackSubtitle => fallbackSubtitle;
        public string AudioEventId => audioEventId;
        public double CooldownGameSeconds => cooldownGameSeconds;
        public IReadOnlyList<NpcDialogueCondition> Conditions =>
            conditions ?? Array.Empty<NpcDialogueCondition>();
        public string EmittedEventId => emittedEventId;
    }

    [Serializable]
    public sealed class NpcDialogueDefinition
    {
        [SerializeField] private string dialogueId = string.Empty;
        [SerializeField] private string characterDefinitionId = string.Empty;
        [SerializeField] private NpcDialogueLine[] lines =
            Array.Empty<NpcDialogueLine>();

        public NpcDialogueDefinition(
            string id,
            string characterId,
            IEnumerable<NpcDialogueLine> configuredLines)
        {
            dialogueId = id ?? string.Empty;
            characterDefinitionId = characterId ?? string.Empty;
            lines = (configuredLines ?? Enumerable.Empty<NpcDialogueLine>())
                .ToArray();
        }

        public string DialogueId => dialogueId;
        public string CharacterDefinitionId => characterDefinitionId;
        public IReadOnlyList<NpcDialogueLine> Lines =>
            lines ?? Array.Empty<NpcDialogueLine>();
    }

    [CreateAssetMenu(
        fileName = "NpcDialogueCatalog",
        menuName = "MSC/NPC/Dialogue Catalog")]
    public sealed class NpcDialogueCatalog : ScriptableObject
    {
        [SerializeField] private NpcDialogueDefinition[] definitions =
            Array.Empty<NpcDialogueDefinition>();

        public IReadOnlyList<NpcDialogueDefinition> Definitions =>
            definitions ?? Array.Empty<NpcDialogueDefinition>();

        public bool TryGetForCharacter(
            string characterDefinitionId,
            out NpcDialogueDefinition definition)
        {
            definition = Definitions.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(
                    candidate.CharacterDefinitionId,
                    characterDefinitionId,
                    StringComparison.Ordinal));
            return definition != null;
        }

        public IReadOnlyList<string> ValidateConfiguration(
            CharacterDefinitionCatalog characters)
        {
            var failures = new List<string>();
            if (characters == null)
            {
                failures.Add("NPC dialogue catalog requires a character catalog.");
                return failures;
            }

            IReadOnlyList<NpcDialogueDefinition> configured = Definitions;
            if (configured.Count > 512)
            {
                failures.Add("NPC dialogue catalog exceeds 512 definitions.");
            }

            var dialogueIds = new HashSet<string>(StringComparer.Ordinal);
            var characterIds = new HashSet<string>(StringComparer.Ordinal);
            var lineIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (NpcDialogueDefinition definition in configured)
            {
                if (definition == null)
                {
                    failures.Add("NPC dialogue catalog contains a null definition.");
                    continue;
                }

                if (!CharacterStableId.TryValidate(
                        definition.DialogueId,
                        "dialogue.",
                        out string failure) ||
                    !dialogueIds.Add(definition.DialogueId))
                {
                    failures.Add(string.IsNullOrEmpty(failure)
                        ? $"Duplicate dialogue ID '{definition.DialogueId}'."
                        : failure);
                }

                if (!CharacterStableId.TryValidate(
                        definition.CharacterDefinitionId,
                        "character.",
                        out failure) ||
                    !characters.TryGet(
                        definition.CharacterDefinitionId,
                        out _) ||
                    !characterIds.Add(definition.CharacterDefinitionId))
                {
                    failures.Add(string.IsNullOrEmpty(failure)
                        ? $"Dialogue '{definition.DialogueId}' has an unknown or duplicate character binding."
                        : failure);
                }

                if (definition.Lines.Count == 0 || definition.Lines.Count > 256)
                {
                    failures.Add(
                        $"Dialogue '{definition.DialogueId}' must contain 1-256 lines.");
                }

                foreach (NpcDialogueLine line in definition.Lines)
                {
                    ValidateLine(definition, line, lineIds, failures);
                }
            }

            return failures;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            IEnumerable<NpcDialogueDefinition> configuredDefinitions)
        {
            definitions = (configuredDefinitions ??
                    Enumerable.Empty<NpcDialogueDefinition>())
                .OrderBy(definition => definition.DialogueId, StringComparer.Ordinal)
                .ToArray();
        }
#endif

        private static void ValidateLine(
            NpcDialogueDefinition definition,
            NpcDialogueLine line,
            ISet<string> lineIds,
            ICollection<string> failures)
        {
            if (line == null)
            {
                failures.Add(
                    $"Dialogue '{definition.DialogueId}' contains a null line.");
                return;
            }

            if (!CharacterStableId.TryValidate(
                    line.LineId,
                    "line.",
                    out string failure) ||
                !lineIds.Add(line.LineId))
            {
                failures.Add(string.IsNullOrEmpty(failure)
                    ? $"Duplicate dialogue line ID '{line.LineId}'."
                    : failure);
            }

            if (!CharacterStableId.TryValidate(
                    line.LocalizationKey,
                    "npc.",
                    out failure) ||
                string.IsNullOrWhiteSpace(line.FallbackSubtitle) ||
                !CharacterStableId.TryValidate(
                    line.AudioEventId,
                    "audio.",
                    out failure) ||
                !double.IsFinite(line.CooldownGameSeconds) ||
                line.CooldownGameSeconds < 0d ||
                !string.IsNullOrEmpty(line.EmittedEventId) &&
                !CharacterStableId.TryValidate(
                    line.EmittedEventId,
                    "event.",
                    out failure))
            {
                failures.Add(string.IsNullOrEmpty(failure)
                    ? $"Dialogue line '{line.LineId}' has invalid presentation or cooldown data."
                    : failure);
            }

            if (line.Conditions.Count > 16)
            {
                failures.Add(
                    $"Dialogue line '{line.LineId}' exceeds 16 conditions.");
            }

            foreach (NpcDialogueCondition condition in line.Conditions)
            {
                string prefix = condition?.Kind switch
                {
                    NpcDialogueConditionKind.CharacterFlag => "flag.",
                    NpcDialogueConditionKind.RelationshipAtLeast =>
                        "relationship.",
                    NpcDialogueConditionKind.ExternalFlag => "flag.",
                    NpcDialogueConditionKind.ActiveScheduleBlock => "schedule.",
                    _ => string.Empty,
                };
                if (condition == null ||
                    string.IsNullOrEmpty(prefix) ||
                    !CharacterStableId.TryValidate(
                        condition.ConditionId,
                        prefix,
                        out failure) ||
                    condition.Kind ==
                        NpcDialogueConditionKind.RelationshipAtLeast &&
                    (condition.Threshold < -100 || condition.Threshold > 100))
                {
                    failures.Add(
                        $"Dialogue line '{line.LineId}' contains an invalid condition.");
                }
            }
        }
    }

    public interface INpcExternalConditionSource
    {
        bool GetFlag(string flagId);
    }

    /// <summary>
    /// Explicit boundary for future 10B event, phone and job owners. Dialogue
    /// emits stable project IDs but does not mutate those domains itself.
    /// </summary>
    public interface INpcDomainEventSink
    {
        void Emit(string eventId, string characterDefinitionId);
    }

    public sealed class NpcDialogueRuntime
    {
        private readonly INpcExternalConditionSource externalConditions;
        private readonly INpcDomainEventSink eventSink;
        private readonly Dictionary<string, int> selectionCursors =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public NpcDialogueRuntime(
            INpcExternalConditionSource configuredExternalConditions = null,
            INpcDomainEventSink configuredEventSink = null)
        {
            externalConditions = configuredExternalConditions;
            eventSink = configuredEventSink;
        }

        public bool TrySelectLine(
            NpcDialogueDefinition definition,
            CharacterInstance character,
            out NpcDialogueLine line)
            => TrySelectLine(
                definition,
                character,
                elapsedGameSeconds: 0d,
                out line);

        public bool TrySelectLine(
            NpcDialogueDefinition definition,
            CharacterInstance character,
            double elapsedGameSeconds,
            out NpcDialogueLine line)
        {
            if (!double.IsFinite(elapsedGameSeconds) || elapsedGameSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedGameSeconds));
            }

            if (definition == null || character == null ||
                !string.Equals(
                    definition.CharacterDefinitionId,
                    character.Definition.DefinitionId,
                    StringComparison.Ordinal))
            {
                line = null;
                return false;
            }

            NpcDialogueLine[] eligible = definition.Lines
                .Where(candidate =>
                    candidate != null &&
                    character.IsDialogueLineEligible(
                        candidate.LineId,
                        elapsedGameSeconds) &&
                    candidate.Conditions.All(condition =>
                        Evaluate(condition, character)))
                .ToArray();
            if (eligible.Length == 0)
            {
                line = null;
                return false;
            }

            string cursorKey = BuildCursorKey(definition, character);
            int cursor = selectionCursors.TryGetValue(cursorKey, out int value)
                ? value
                : 0;
            selectionCursors[cursorKey] = cursor;
            line = eligible[cursor % eligible.Length];
            return true;
        }

        public void CommitLine(
            NpcDialogueLine line,
            CharacterInstance character)
            => CommitLine(line, character, elapsedGameSeconds: 0d);

        public void CommitLine(
            NpcDialogueLine line,
            CharacterInstance character,
            double elapsedGameSeconds)
        {
            if (line == null || character == null)
            {
                throw new ArgumentNullException(
                    line == null ? nameof(line) : nameof(character));
            }

            character.RecordDialogueLine(
                line.LineId,
                elapsedGameSeconds,
                line.CooldownGameSeconds);

            string cursorPrefix = character.StableInstanceId + ":";
            string[] matchingKeys = selectionCursors.Keys
                .Where(key => key.StartsWith(cursorPrefix, StringComparison.Ordinal))
                .ToArray();
            for (int index = 0; index < matchingKeys.Length; index++)
            {
                selectionCursors[matchingKeys[index]] =
                    selectionCursors[matchingKeys[index]] == int.MaxValue
                        ? 0
                        : selectionCursors[matchingKeys[index]] + 1;
            }

            if (!string.IsNullOrEmpty(line.EmittedEventId))
            {
                eventSink?.Emit(
                    line.EmittedEventId,
                    character.Definition.DefinitionId);
            }
        }

        private bool Evaluate(
            NpcDialogueCondition condition,
            CharacterInstance character)
        {
            if (condition == null)
            {
                return false;
            }

            switch (condition.Kind)
            {
                case NpcDialogueConditionKind.CharacterFlag:
                    return character.GetFlag(condition.ConditionId) ==
                           condition.ExpectedValue;
                case NpcDialogueConditionKind.RelationshipAtLeast:
                    return character.GetRelationship(condition.ConditionId) >=
                           condition.Threshold;
                case NpcDialogueConditionKind.ExternalFlag:
                    return externalConditions != null &&
                           externalConditions.GetFlag(condition.ConditionId) ==
                           condition.ExpectedValue;
                case NpcDialogueConditionKind.ActiveScheduleBlock:
                    return string.Equals(
                               character.ActiveScheduleBlockId,
                               condition.ConditionId,
                               StringComparison.Ordinal) ==
                           condition.ExpectedValue;
                default:
                    return false;
            }
        }

        private static string BuildCursorKey(
            NpcDialogueDefinition definition,
            CharacterInstance character) =>
            character.StableInstanceId + ":" + definition.DialogueId;
    }
}
