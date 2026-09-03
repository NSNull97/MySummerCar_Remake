using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Items
{
    [Serializable]
    public sealed class ItemScalarState
    {
        public string stateId = string.Empty;
        public float value;

        public ItemScalarState DeepClone() => (ItemScalarState)MemberwiseClone();
    }

    [Serializable]
    public sealed class ItemFlagState
    {
        public string stateId = string.Empty;
        public bool value;

        public ItemFlagState DeepClone() => (ItemFlagState)MemberwiseClone();
    }

    [Serializable]
    public sealed class ItemInstanceState
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string stableEntityId = string.Empty;
        public string definitionId = string.Empty;
        public float content;
        public float condition = 100f;
        public bool isOpen;
        public bool isBroken;
        public bool isEnabled;
        public bool isConsumed;
        public int variantIndex;
        public float cookingSeconds;
        public double lastFoodSimulationGameSeconds;
        public bool foodSimulationInitialized;
        public string liquidId = string.Empty;
        public string[] containedStableIds = Array.Empty<string>();
        public ItemScalarState[] scalarStates = Array.Empty<ItemScalarState>();
        public ItemFlagState[] flagStates = Array.Empty<ItemFlagState>();

        public bool IsEmpty => content <= 0.0001f &&
                               (containedStableIds?.Length ?? 0) == 0;

        public ItemInstanceState DeepClone()
        {
            return new ItemInstanceState
            {
                schemaVersion = schemaVersion,
                stableEntityId = stableEntityId,
                definitionId = definitionId,
                content = content,
                condition = condition,
                isOpen = isOpen,
                isBroken = isBroken,
                isEnabled = isEnabled,
                isConsumed = isConsumed,
                variantIndex = variantIndex,
                cookingSeconds = cookingSeconds,
                lastFoodSimulationGameSeconds =
                    lastFoodSimulationGameSeconds,
                foodSimulationInitialized = foodSimulationInitialized,
                liquidId = liquidId,
                containedStableIds = containedStableIds != null
                    ? (string[])containedStableIds.Clone()
                    : Array.Empty<string>(),
                scalarStates = (scalarStates ?? Array.Empty<ItemScalarState>())
                    .Select(state => state?.DeepClone())
                    .ToArray(),
                flagStates = (flagStates ?? Array.Empty<ItemFlagState>())
                    .Select(state => state?.DeepClone())
                    .ToArray(),
            };
        }

        public bool TryValidate(
            ItemDefinitionRecord definition,
            out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion ||
                !StableEntityId.TryParse(stableEntityId, out _) ||
                definition == null ||
                !string.Equals(
                    definitionId,
                    definition.DefinitionId,
                    StringComparison.Ordinal) ||
                !float.IsFinite(content) ||
                content < -0.0001f ||
                content > definition.MaximumContent + 0.0001f ||
                !float.IsFinite(condition) ||
                condition < 0f ||
                condition > 100f ||
                !float.IsFinite(cookingSeconds) ||
                cookingSeconds < 0f ||
                cookingSeconds > Math.Max(
                    86400f,
                    definition.Food.MaximumCookingSeconds + 86400f) ||
                !double.IsFinite(lastFoodSimulationGameSeconds) ||
                lastFoodSimulationGameSeconds < 0d ||
                variantIndex < 0 ||
                variantIndex >= Math.Max(1, definition.ToolVariants.Count) ||
                liquidId == null ||
                liquidId.Length > 96 ||
                containedStableIds == null ||
                containedStableIds.Length > 256 ||
                scalarStates == null || scalarStates.Length > 128 ||
                flagStates == null || flagStates.Length > 128)
            {
                failure = $"Item state '{stableEntityId}' is invalid for " +
                          $"definition '{definition?.DefinitionId}'.";
                return false;
            }

            var childIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string childId in containedStableIds)
            {
                if (!StableEntityId.TryParse(childId, out _) ||
                    !childIds.Add(childId) ||
                    string.Equals(childId, stableEntityId, StringComparison.Ordinal))
                {
                    failure = $"Item '{stableEntityId}' has invalid contained identities.";
                    return false;
                }
            }

            if (definition.InitialChildCount > 0 &&
                Mathf.Abs(content - containedStableIds.Length) > 0.0001f)
            {
                failure = $"Container '{stableEntityId}' content does not match " +
                          "its remaining physical child identities.";
                return false;
            }

            if (definition.SupportsLiquidTransfer &&
                ((content > 0.0001f && string.IsNullOrWhiteSpace(liquidId)) ||
                 (content <= 0.0001f && !string.IsNullOrEmpty(liquidId))))
            {
                failure = $"Liquid state for item '{stableEntityId}' is inconsistent.";
                return false;
            }

            var scalarById = definition.ScalarStates.ToDictionary(
                state => state.StateId,
                StringComparer.Ordinal);
            var observedStateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemScalarState state in scalarStates)
            {
                if (state == null ||
                    !scalarById.TryGetValue(
                        state.stateId ?? string.Empty,
                        out ItemScalarDefinition stateDefinition) ||
                    !float.IsFinite(state.value) ||
                    state.value < stateDefinition.Minimum ||
                    state.value > stateDefinition.Maximum ||
                    !observedStateIds.Add(state.stateId))
                {
                    failure = $"Item '{stableEntityId}' has invalid scalar state.";
                    return false;
                }
            }

            var flagIds = new HashSet<string>(
                definition.FlagStates.Select(state => state.StateId),
                StringComparer.Ordinal);
            foreach (ItemFlagState state in flagStates)
            {
                if (state == null ||
                    !flagIds.Contains(state.stateId ?? string.Empty) ||
                    !observedStateIds.Add(state.stateId))
                {
                    failure = $"Item '{stableEntityId}' has invalid flag state.";
                    return false;
                }
            }

            if (scalarStates.Length != scalarById.Count ||
                flagStates.Length != flagIds.Count)
            {
                failure = $"Item '{stableEntityId}' is missing definition-owned state.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public static ItemInstanceState CreateInitial(
            ItemDefinitionRecord definition,
            StableEntityId stableId,
            int variantIndex = 0)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!stableId.IsValid)
            {
                throw new ArgumentException(
                    "Initial item identity is invalid.",
                    nameof(stableId));
            }

            string[] childIds = new string[definition.InitialChildCount];
            for (int index = 0; index < childIds.Length; index++)
            {
                childIds[index] = ItemStableIdUtility.CreateDeterministic(
                    $"{stableId.Value}|contained|{index:000}").Value;
            }

            var state = new ItemInstanceState
            {
                stableEntityId = stableId.Value,
                definitionId = definition.DefinitionId,
                content = definition.InitialContent,
                condition = definition.Food.Perishable &&
                            definition.ContentMeasure ==
                                ItemContentMeasure.Condition
                    ? Mathf.Clamp(definition.InitialContent, 0f, 100f)
                    : 100f,
                cookingSeconds = 0f,
                lastFoodSimulationGameSeconds = 0d,
                foodSimulationInitialized = false,
                isOpen = definition.StartsOpen,
                variantIndex = variantIndex,
                liquidId = definition.InitialLiquidId,
                containedStableIds = childIds,
                scalarStates = definition.ScalarStates
                    .Select(value => new ItemScalarState
                    {
                        stateId = value.StateId,
                        value = value.InitialValue,
                    })
                    .ToArray(),
                flagStates = definition.FlagStates
                    .Select(value => new ItemFlagState
                    {
                        stateId = value.StateId,
                        value = value.InitialValue,
                    })
                    .ToArray(),
            };
            if (!state.TryValidate(definition, out string failure))
            {
                throw new InvalidOperationException(failure);
            }

            return state;
        }
    }
}
