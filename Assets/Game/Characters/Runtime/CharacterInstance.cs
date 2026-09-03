using System;
using System.Collections.Generic;
using System.Linq;

namespace MSC.Characters
{
    [Serializable]
    public sealed class CharacterFlagState
    {
        public string flagId = string.Empty;
        public bool value;

        public CharacterFlagState DeepClone() =>
            (CharacterFlagState)MemberwiseClone();
    }

    [Serializable]
    public sealed class CharacterRelationshipState
    {
        public string relationshipId = string.Empty;
        public int value;

        public CharacterRelationshipState DeepClone() =>
            (CharacterRelationshipState)MemberwiseClone();
    }

    [Serializable]
    public sealed class CharacterDialogueCooldownState
    {
        public string lineId = string.Empty;
        public double nextEligibleGameSeconds;

        public CharacterDialogueCooldownState DeepClone() =>
            (CharacterDialogueCooldownState)MemberwiseClone();
    }

    [Serializable]
    public sealed class CharacterInstanceSnapshot
    {
        public string definitionId = string.Empty;
        public string stableInstanceId = string.Empty;
        public string activeScheduleBlockId = string.Empty;
        public string currentAnchorId = string.Empty;
        public string currentRouteId = string.Empty;
        public double routeProgress01;
        public CharacterActivityState activityState = CharacterActivityState.Hidden;
        public CharacterFlagState[] flags = Array.Empty<CharacterFlagState>();
        public CharacterRelationshipState[] relationships =
            Array.Empty<CharacterRelationshipState>();
        public CharacterDialogueCooldownState[] dialogueCooldowns =
            Array.Empty<CharacterDialogueCooldownState>();

        public CharacterInstanceSnapshot DeepClone()
        {
            return new CharacterInstanceSnapshot
            {
                definitionId = definitionId,
                stableInstanceId = stableInstanceId,
                activeScheduleBlockId = activeScheduleBlockId,
                currentAnchorId = currentAnchorId,
                currentRouteId = currentRouteId,
                routeProgress01 = routeProgress01,
                activityState = activityState,
                flags = (flags ?? Array.Empty<CharacterFlagState>())
                    .Select(flag => flag?.DeepClone())
                    .ToArray(),
                relationships =
                    (relationships ??
                        Array.Empty<CharacterRelationshipState>())
                    .Select(relationship => relationship?.DeepClone())
                    .ToArray(),
                dialogueCooldowns =
                    (dialogueCooldowns ??
                        Array.Empty<CharacterDialogueCooldownState>())
                    .Select(cooldown => cooldown?.DeepClone())
                    .ToArray(),
            };
        }
    }

    /// <summary>
    /// Mutable project-owned character state. It contains no Unity object or
    /// donor hierarchy identity and therefore remains valid while presentation
    /// cells are unloaded.
    /// </summary>
    public sealed class CharacterInstance
    {
        private readonly Dictionary<string, bool> flags =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> relationships =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> dialogueCooldowns =
            new Dictionary<string, double>(StringComparer.Ordinal);

        public CharacterInstance(CharacterDefinition definition)
        {
            Definition = definition ??
                throw new ArgumentNullException(nameof(definition));
            if (!definition.TryValidate(out string failure))
            {
                throw new ArgumentException(failure, nameof(definition));
            }

            ActiveScheduleBlockId = string.Empty;
            CurrentAnchorId = definition.HomeAnchorId;
            CurrentRouteId = string.Empty;
            ActivityState = CharacterActivityState.Hidden;
        }

        public CharacterDefinition Definition { get; }
        public string StableInstanceId => Definition.StableInstanceId;
        public string ActiveScheduleBlockId { get; private set; }
        public string CurrentAnchorId { get; private set; }
        public string CurrentRouteId { get; private set; }
        public double RouteProgress01 { get; private set; }
        public CharacterActivityState ActivityState { get; private set; }

        public void ApplyScheduleState(
            string scheduleBlockId,
            string anchorId,
            string routeId,
            double routeProgress01,
            CharacterActivityState activityState)
        {
            if (!string.IsNullOrEmpty(scheduleBlockId) &&
                !CharacterStableId.TryValidate(
                    scheduleBlockId,
                    "schedule.",
                    out string failure))
            {
                throw new ArgumentException(failure, nameof(scheduleBlockId));
            }

            if (!CharacterStableId.TryValidate(
                    anchorId,
                    "anchor.",
                    out failure))
            {
                throw new ArgumentException(failure, nameof(anchorId));
            }

            if (!string.IsNullOrEmpty(routeId) &&
                !CharacterStableId.TryValidate(
                    routeId,
                    "route.",
                    out failure))
            {
                throw new ArgumentException(failure, nameof(routeId));
            }

            if (!double.IsFinite(routeProgress01) ||
                routeProgress01 < 0d || routeProgress01 > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(routeProgress01));
            }

            if (!Enum.IsDefined(typeof(CharacterActivityState), activityState))
            {
                throw new ArgumentOutOfRangeException(nameof(activityState));
            }

            ActiveScheduleBlockId = scheduleBlockId ?? string.Empty;
            CurrentAnchorId = anchorId;
            CurrentRouteId = routeId ?? string.Empty;
            RouteProgress01 = routeProgress01;
            ActivityState = activityState;
        }

        public void ApplyPhysicalRouteProgress(double routeProgress01)
        {
            if (string.IsNullOrEmpty(CurrentRouteId))
            {
                throw new InvalidOperationException(
                    "Physical route progress requires an active route.");
            }

            if (!double.IsFinite(routeProgress01) ||
                routeProgress01 < 0d || routeProgress01 > 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(routeProgress01));
            }

            RouteProgress01 = routeProgress01;
        }

        public bool GetFlag(string flagId)
        {
            ValidateFlagId(flagId);
            return flags.TryGetValue(flagId, out bool value) && value;
        }

        public void SetFlag(string flagId, bool value)
        {
            ValidateFlagId(flagId);
            flags[flagId] = value;
        }

        public int GetRelationship(string relationshipId)
        {
            ValidateRelationshipId(relationshipId);
            return relationships.TryGetValue(relationshipId, out int value)
                ? value
                : 0;
        }

        public void SetRelationship(string relationshipId, int value)
        {
            ValidateRelationshipId(relationshipId);
            if (value < -100 || value > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            relationships[relationshipId] = value;
        }

        public bool IsDialogueLineEligible(
            string lineId,
            double elapsedGameSeconds)
        {
            ValidateLineId(lineId);
            if (!double.IsFinite(elapsedGameSeconds) || elapsedGameSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedGameSeconds));
            }

            return !dialogueCooldowns.TryGetValue(
                       lineId,
                       out double nextEligible) ||
                   elapsedGameSeconds >= nextEligible;
        }

        public void RecordDialogueLine(
            string lineId,
            double elapsedGameSeconds,
            double cooldownGameSeconds)
        {
            ValidateLineId(lineId);
            if (!double.IsFinite(elapsedGameSeconds) || elapsedGameSeconds < 0d ||
                !double.IsFinite(cooldownGameSeconds) || cooldownGameSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedGameSeconds));
            }

            dialogueCooldowns[lineId] =
                elapsedGameSeconds + cooldownGameSeconds;
        }

        public CharacterInstanceSnapshot CaptureSnapshot()
        {
            return new CharacterInstanceSnapshot
            {
                definitionId = Definition.DefinitionId,
                stableInstanceId = StableInstanceId,
                activeScheduleBlockId = ActiveScheduleBlockId,
                currentAnchorId = CurrentAnchorId,
                currentRouteId = CurrentRouteId,
                routeProgress01 = RouteProgress01,
                activityState = ActivityState,
                flags = flags
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new CharacterFlagState
                    {
                        flagId = pair.Key,
                        value = pair.Value,
                    })
                    .ToArray(),
                relationships = relationships
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new CharacterRelationshipState
                    {
                        relationshipId = pair.Key,
                        value = pair.Value,
                    })
                    .ToArray(),
                dialogueCooldowns = dialogueCooldowns
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new CharacterDialogueCooldownState
                    {
                        lineId = pair.Key,
                        nextEligibleGameSeconds = pair.Value,
                    })
                    .ToArray(),
            };
        }

        public bool TryRestoreSnapshot(
            CharacterInstanceSnapshot snapshot,
            out string failure)
        {
            if (!TryValidateSnapshot(snapshot, Definition, out failure))
            {
                return false;
            }

            var restoredFlags = new Dictionary<string, bool>(
                StringComparer.Ordinal);
            foreach (CharacterFlagState flag in
                     snapshot.flags ?? Array.Empty<CharacterFlagState>())
            {
                restoredFlags.Add(flag.flagId, flag.value);
            }

            var restoredRelationships = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (CharacterRelationshipState relationship in
                     snapshot.relationships ??
                     Array.Empty<CharacterRelationshipState>())
            {
                restoredRelationships.Add(
                    relationship.relationshipId,
                    relationship.value);
            }

            var restoredCooldowns = new Dictionary<string, double>(
                StringComparer.Ordinal);
            foreach (CharacterDialogueCooldownState cooldown in
                     snapshot.dialogueCooldowns ??
                     Array.Empty<CharacterDialogueCooldownState>())
            {
                restoredCooldowns.Add(
                    cooldown.lineId,
                    cooldown.nextEligibleGameSeconds);
            }

            ActiveScheduleBlockId = snapshot.activeScheduleBlockId;
            CurrentAnchorId = snapshot.currentAnchorId;
            CurrentRouteId = snapshot.currentRouteId;
            RouteProgress01 = snapshot.routeProgress01;
            ActivityState = snapshot.activityState;
            flags.Clear();
            relationships.Clear();
            dialogueCooldowns.Clear();
            foreach (KeyValuePair<string, bool> pair in restoredFlags)
            {
                flags.Add(pair.Key, pair.Value);
            }

            foreach (KeyValuePair<string, int> pair in restoredRelationships)
            {
                relationships.Add(pair.Key, pair.Value);
            }


            foreach (KeyValuePair<string, double> pair in restoredCooldowns)
            {
                dialogueCooldowns.Add(pair.Key, pair.Value);
            }

            failure = string.Empty;
            return true;
        }

        public static bool TryValidateSnapshot(
            CharacterInstanceSnapshot snapshot,
            CharacterDefinition expectedDefinition,
            out string failure)
        {
            if (snapshot == null || expectedDefinition == null)
            {
                failure = "Character snapshot and definition are required.";
                return false;
            }

            if (!string.Equals(
                    snapshot.definitionId,
                    expectedDefinition.DefinitionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.stableInstanceId,
                    expectedDefinition.StableInstanceId,
                    StringComparison.Ordinal))
            {
                failure =
                    $"Character snapshot identity does not match '{expectedDefinition.DefinitionId}'.";
                return false;
            }

            if (!string.IsNullOrEmpty(snapshot.activeScheduleBlockId) &&
                !CharacterStableId.TryValidate(
                    snapshot.activeScheduleBlockId,
                    "schedule.",
                    out failure) ||
                !CharacterStableId.TryValidate(
                    snapshot.currentAnchorId,
                    "anchor.",
                    out failure) ||
                !string.IsNullOrEmpty(snapshot.currentRouteId) &&
                !CharacterStableId.TryValidate(
                    snapshot.currentRouteId,
                    "route.",
                    out failure))
            {
                return false;
            }

            if (!double.IsFinite(snapshot.routeProgress01) ||
                snapshot.routeProgress01 < 0d ||
                snapshot.routeProgress01 > 1d ||
                !Enum.IsDefined(
                    typeof(CharacterActivityState),
                    snapshot.activityState))
            {
                failure = "Character snapshot contains invalid route/activity state.";
                return false;
            }

            var flagIds = new HashSet<string>(StringComparer.Ordinal);
            CharacterFlagState[] snapshotFlags =
                snapshot.flags ?? Array.Empty<CharacterFlagState>();
            if (snapshotFlags.Length > 128)
            {
                failure = "Character snapshot contains too many flags.";
                return false;
            }

            foreach (CharacterFlagState flag in snapshotFlags)
            {
                if (flag == null ||
                    !CharacterStableId.TryValidate(
                        flag.flagId,
                        "flag.",
                        out failure) ||
                    !flagIds.Add(flag.flagId))
                {
                    failure = string.IsNullOrEmpty(failure)
                        ? "Character snapshot contains duplicate flags."
                        : failure;
                    return false;
                }
            }

            var relationshipIds = new HashSet<string>(StringComparer.Ordinal);
            CharacterRelationshipState[] snapshotRelationships =
                snapshot.relationships ??
                Array.Empty<CharacterRelationshipState>();
            if (snapshotRelationships.Length > 64)
            {
                failure = "Character snapshot contains too many relationships.";
                return false;
            }

            foreach (CharacterRelationshipState relationship in
                     snapshotRelationships)
            {
                if (relationship == null ||
                    !CharacterStableId.TryValidate(
                        relationship.relationshipId,
                        "relationship.",
                        out failure) ||
                    !relationshipIds.Add(relationship.relationshipId) ||
                    relationship.value < -100 ||
                    relationship.value > 100)
                {
                    failure = string.IsNullOrEmpty(failure)
                        ? "Character snapshot contains duplicate or invalid relationships."
                        : failure;
                    return false;
                }
            }


            var cooldownLineIds = new HashSet<string>(StringComparer.Ordinal);
            CharacterDialogueCooldownState[] snapshotCooldowns =
                snapshot.dialogueCooldowns ??
                Array.Empty<CharacterDialogueCooldownState>();
            if (snapshotCooldowns.Length > 256)
            {
                failure = "Character snapshot contains too many dialogue cooldowns.";
                return false;
            }

            foreach (CharacterDialogueCooldownState cooldown in snapshotCooldowns)
            {
                if (cooldown == null ||
                    !CharacterStableId.TryValidate(
                        cooldown.lineId,
                        "line.",
                        out failure) ||
                    !cooldownLineIds.Add(cooldown.lineId) ||
                    !double.IsFinite(cooldown.nextEligibleGameSeconds) ||
                    cooldown.nextEligibleGameSeconds < 0d)
                {
                    failure = string.IsNullOrEmpty(failure)
                        ? "Character snapshot contains duplicate or invalid dialogue cooldowns."
                        : failure;
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static void ValidateFlagId(string flagId)
        {
            if (!CharacterStableId.TryValidate(flagId, "flag.", out string failure))
            {
                throw new ArgumentException(failure, nameof(flagId));
            }
        }

        private static void ValidateRelationshipId(string relationshipId)
        {
            if (!CharacterStableId.TryValidate(
                    relationshipId,
                    "relationship.",
                    out string failure))
            {
                throw new ArgumentException(failure, nameof(relationshipId));
            }
        }

        private static void ValidateLineId(string lineId)
        {
            if (!CharacterStableId.TryValidate(
                    lineId,
                    "line.",
                    out string failure))
            {
                throw new ArgumentException(failure, nameof(lineId));
            }
        }
    }
}
