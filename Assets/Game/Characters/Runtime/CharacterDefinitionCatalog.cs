using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Characters
{
    public enum CharacterFixturePattern
    {
        StationaryService = 0,
        ScheduledRoaming = 1,
        VehicleLinked = 2,
    }

    public enum CharacterActivityState
    {
        Hidden = 0,
        Idle = 1,
        Working = 2,
        Walking = 3,
        Talking = 4,
        VehicleSeated = 5,
        Disabled = 6,
    }

    [Serializable]
    public sealed class CharacterDefinition
    {
        [SerializeField] private string definitionId = string.Empty;
        [SerializeField] private string featureId = string.Empty;
        [SerializeField] private string debugDisplayRole = string.Empty;
        [SerializeField] private string stableInstanceId = string.Empty;
        [SerializeField] private string presentationBindingId = string.Empty;
        [SerializeField] private string productionReplacementKey = string.Empty;
        [SerializeField] private string homeAnchorId = string.Empty;
        [SerializeField] private string workAnchorId = string.Empty;
        [SerializeField] private CharacterFixturePattern fixturePattern;
        [SerializeField] private bool frameworkFixtureOnly = true;
        [SerializeField] private bool stateOnly;

        public CharacterDefinition(
            string configuredDefinitionId,
            string configuredFeatureId,
            string configuredDebugDisplayRole,
            string configuredStableInstanceId,
            string configuredPresentationBindingId,
            string configuredProductionReplacementKey,
            string configuredHomeAnchorId,
            string configuredWorkAnchorId,
            CharacterFixturePattern configuredFixturePattern,
            bool isFrameworkFixtureOnly,
            bool isStateOnly = false)
        {
            definitionId = configuredDefinitionId ?? string.Empty;
            featureId = configuredFeatureId ?? string.Empty;
            debugDisplayRole = configuredDebugDisplayRole ?? string.Empty;
            stableInstanceId = configuredStableInstanceId ?? string.Empty;
            presentationBindingId = configuredPresentationBindingId ?? string.Empty;
            productionReplacementKey = configuredProductionReplacementKey ?? string.Empty;
            homeAnchorId = configuredHomeAnchorId ?? string.Empty;
            workAnchorId = configuredWorkAnchorId ?? string.Empty;
            fixturePattern = configuredFixturePattern;
            frameworkFixtureOnly = isFrameworkFixtureOnly;
            stateOnly = isStateOnly;
        }

        public string DefinitionId => definitionId;
        public string FeatureId => featureId;
        public string DebugDisplayRole => debugDisplayRole;
        public string StableInstanceId => stableInstanceId;
        public string PresentationBindingId => presentationBindingId;
        public string ProductionReplacementKey => productionReplacementKey;
        public string HomeAnchorId => homeAnchorId;
        public string WorkAnchorId => workAnchorId;
        public CharacterFixturePattern FixturePattern => fixturePattern;
        public bool FrameworkFixtureOnly => frameworkFixtureOnly;
        public bool StateOnly => stateOnly;

        public bool TryValidate(out string failure)
        {
            if (!CharacterStableId.TryValidate(definitionId, "character.", out failure))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(featureId) ||
                !featureId.StartsWith("P1.NPC.", StringComparison.Ordinal))
            {
                failure = $"Character '{definitionId}' has an invalid Phase 1 feature ID.";
                return false;
            }

            if (!StableEntityId.TryParse(stableInstanceId, out _))
            {
                failure = $"Character '{definitionId}' has an invalid stable instance ID.";
                return false;
            }

            if (!CharacterStableId.TryValidate(
                    presentationBindingId,
                    "presentation.character.",
                    out failure) ||
                !CharacterStableId.TryValidate(
                    productionReplacementKey,
                    "presentation.character.",
                    out failure) ||
                !CharacterStableId.TryValidate(homeAnchorId, "anchor.", out failure) ||
                !CharacterStableId.TryValidate(workAnchorId, "anchor.", out failure))
            {
                failure = $"Character '{definitionId}': {failure}";
                return false;
            }

            if (!Enum.IsDefined(typeof(CharacterFixturePattern), fixturePattern))
            {
                failure = $"Character '{definitionId}' has an unsupported fixture pattern.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [CreateAssetMenu(
        fileName = "CharacterDefinitionCatalog",
        menuName = "MSC/Characters/Character Definition Catalog")]
    public sealed class CharacterDefinitionCatalog : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string catalogId = "characters.phase1.foundation.v1";
        [SerializeField] private CharacterDefinition[] definitions =
            Array.Empty<CharacterDefinition>();

        public int SchemaVersion => schemaVersion;
        public string CatalogId => catalogId;
        public IReadOnlyList<CharacterDefinition> Definitions =>
            definitions ?? Array.Empty<CharacterDefinition>();

        public bool TryGet(string definitionId, out CharacterDefinition definition)
        {
            CharacterDefinition[] configured =
                definitions ?? Array.Empty<CharacterDefinition>();
            for (int index = 0; index < configured.Length; index++)
            {
                if (string.Equals(
                        configured[index]?.DefinitionId,
                        definitionId,
                        StringComparison.Ordinal))
                {
                    definition = configured[index];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var failures = new List<string>();
            if (schemaVersion != CurrentSchemaVersion)
            {
                failures.Add(
                    $"Character catalog schema {schemaVersion} does not match {CurrentSchemaVersion}.");
            }

            if (!CharacterStableId.TryValidate(catalogId, "characters.", out string catalogFailure))
            {
                failures.Add(catalogFailure);
            }

            CharacterDefinition[] configured =
                definitions ?? Array.Empty<CharacterDefinition>();
            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterDefinition definition in configured)
            {
                if (definition == null)
                {
                    failures.Add("Character catalog contains a null definition.");
                    continue;
                }

                if (!definition.TryValidate(out string failure))
                {
                    failures.Add(failure);
                }

                if (!definitionIds.Add(definition.DefinitionId))
                {
                    failures.Add(
                        $"Duplicate character definition ID '{definition.DefinitionId}'.");
                }

                if (!instanceIds.Add(definition.StableInstanceId))
                {
                    failures.Add(
                        $"Duplicate character stable instance ID '{definition.StableInstanceId}'.");
                }
            }

            return failures;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredCatalogId,
            IEnumerable<CharacterDefinition> configuredDefinitions)
        {
            schemaVersion = CurrentSchemaVersion;
            catalogId = configuredCatalogId ?? string.Empty;
            definitions = (configuredDefinitions ??
                    Enumerable.Empty<CharacterDefinition>())
                .OrderBy(definition => definition.DefinitionId, StringComparer.Ordinal)
                .ToArray();
        }
#endif
    }

    public static class CharacterStableId
    {
        public const int MaximumLength = 128;

        public static bool TryValidate(
            string value,
            string requiredPrefix,
            out string failure)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > MaximumLength ||
                !value.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                failure =
                    $"Stable ID must start with '{requiredPrefix}' and contain at most {MaximumLength} characters.";
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') &&
                    character != '.' && character != '-' && character != '_')
                {
                    failure =
                        $"Stable ID '{value}' contains unsupported character '{character}'.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }
    }
}
