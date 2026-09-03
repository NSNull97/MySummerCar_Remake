using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Items
{
    public enum ItemContentMeasure
    {
        None = 0,
        Units = 1,
        Portions = 2,
        Litres = 3,
        Condition = 4,
        Charge = 5,
    }

    public enum ItemPrimaryAction
    {
        None = 0,
        Consume = 1,
        ToggleOpen = 2,
        ToggleDevice = 3,
        CycleVariant = 4,
        DispenseChild = 5,
        ConsumeContainedChild = 6,
        Ignite = 7,
        IgniteFuel = 8,
    }

    public enum ItemCalibrationStatus
    {
        EvidenceBacked = 0,
        Provisional = 1,
        DownstreamFeaturePending = 2,
    }

    [Serializable]
    public sealed class ItemScalarDefinition
    {
        [SerializeField] private string stateId = string.Empty;
        [SerializeField] private float minimum;
        [SerializeField] private float maximum = 1f;
        [SerializeField] private float initialValue;

        public string StateId => stateId;
        public float Minimum => minimum;
        public float Maximum => maximum;
        public float InitialValue => initialValue;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            float configuredMinimum,
            float configuredMaximum,
            float configuredInitial)
        {
            stateId = id ?? string.Empty;
            minimum = configuredMinimum;
            maximum = configuredMaximum;
            initialValue = configuredInitial;
        }
#endif
    }

    [Serializable]
    public sealed class ItemFlagDefinition
    {
        [SerializeField] private string stateId = string.Empty;
        [SerializeField] private bool initialValue;

        public string StateId => stateId;
        public bool InitialValue => initialValue;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(string id, bool configuredInitial)
        {
            stateId = id ?? string.Empty;
            initialValue = configuredInitial;
        }
#endif
    }

    [Serializable]
    public sealed class ItemDefinitionRecord
    {
        [SerializeField] private string definitionId = string.Empty;
        [SerializeField] private string featureId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string category = string.Empty;
        [SerializeField] private string parentFeatureId = string.Empty;
        [SerializeField] private string donorEvidence = string.Empty;
        [SerializeField] private string replacementKey = string.Empty;
        [SerializeField] private bool required = true;
        [SerializeField] private ItemCalibrationStatus calibrationStatus =
            ItemCalibrationStatus.Provisional;
        [SerializeField] private ItemContentMeasure contentMeasure;
        [SerializeField] private ItemPrimaryAction primaryAction;
        [SerializeField, Min(0f)] private float maximumContent;
        [SerializeField, Min(0f)] private float initialContent;
        [SerializeField, Min(0f)] private float useAmount = 1f;
        [SerializeField, Min(0f)] private float massKilograms = 0.5f;
        [SerializeField, Min(0f)] private float emptyContainerMassKilograms;
        [SerializeField, Min(0.01f)] private float maximumCarryMassKilograms = 35f;
        [SerializeField] private Vector3 proxySize = Vector3.one * 0.25f;
        [SerializeField] private bool canOpen;
        [SerializeField] private bool startsOpen;
        [SerializeField] private bool retainWhenEmpty = true;
        [SerializeField] private bool supportsLiquidTransfer;
        [SerializeField] private string initialLiquidId = string.Empty;
        [SerializeField] private bool criticalRecovery;
        [SerializeField] private int initialChildCount;
        [SerializeField] private string childDefinitionId = string.Empty;
        [SerializeField] private string producedDefinitionId = string.Empty;
        [SerializeField] private string toolType = string.Empty;
        [SerializeField] private string[] toolVariants = Array.Empty<string>();
        [SerializeField] private float hungerEffect;
        [SerializeField] private float thirstEffect;
        [SerializeField] private float stressEffect;
        [SerializeField] private float weightEffect;
        [SerializeField] private float intoxicationEffect;
        [SerializeField] private float urineEffect;
        [SerializeField] private float fatigueEffect;
        [SerializeField] private float dirtinessEffect;
        [SerializeField] private ItemScalarDefinition[] scalarStates =
            Array.Empty<ItemScalarDefinition>();
        [SerializeField] private ItemFlagDefinition[] flagStates =
            Array.Empty<ItemFlagDefinition>();
        [SerializeField] private ItemFoodDefinition food =
            new ItemFoodDefinition();
        [SerializeField] private ItemHeatSourceDefinition heatSource =
            new ItemHeatSourceDefinition();
        [SerializeField] private ItemCombustionDefinition combustion =
            new ItemCombustionDefinition();
        [SerializeField] private ItemColliderShapeDefinition[] colliderShapes =
            Array.Empty<ItemColliderShapeDefinition>();

        public string DefinitionId => definitionId;
        public string FeatureId => featureId;
        public string DisplayName => displayName;
        public string Category => category;
        public string ParentFeatureId => parentFeatureId;
        public string DonorEvidence => donorEvidence;
        public string ReplacementKey => replacementKey;
        public bool Required => required;
        public ItemCalibrationStatus CalibrationStatus => calibrationStatus;
        public ItemContentMeasure ContentMeasure => contentMeasure;
        public ItemPrimaryAction PrimaryAction => primaryAction;
        public float MaximumContent => maximumContent;
        public float InitialContent => initialContent;
        public float UseAmount => useAmount;
        public float MassKilograms => massKilograms;
        public float EmptyContainerMassKilograms =>
            emptyContainerMassKilograms;
        public float MaximumCarryMassKilograms => maximumCarryMassKilograms;
        public Vector3 ProxySize => proxySize;
        public bool CanOpen => canOpen;
        public bool StartsOpen => startsOpen;
        public bool RetainWhenEmpty => retainWhenEmpty;
        public bool SupportsLiquidTransfer => supportsLiquidTransfer;
        public string InitialLiquidId => initialLiquidId;
        public bool CriticalRecovery => criticalRecovery;
        public int InitialChildCount => initialChildCount;
        public string ChildDefinitionId => childDefinitionId;
        public string ProducedDefinitionId => producedDefinitionId;
        public string ToolType => toolType;
        public IReadOnlyList<string> ToolVariants =>
            toolVariants ?? Array.Empty<string>();
        public float HungerEffect => hungerEffect;
        public float ThirstEffect => thirstEffect;
        public float StressEffect => stressEffect;
        public float WeightEffect => weightEffect;
        public float IntoxicationEffect => intoxicationEffect;
        public float UrineEffect => urineEffect;
        public float FatigueEffect => fatigueEffect;
        public float DirtinessEffect => dirtinessEffect;
        public IReadOnlyList<ItemScalarDefinition> ScalarStates =>
            scalarStates ?? Array.Empty<ItemScalarDefinition>();
        public IReadOnlyList<ItemFlagDefinition> FlagStates =>
            flagStates ?? Array.Empty<ItemFlagDefinition>();
        public ItemFoodDefinition Food => food ??= new ItemFoodDefinition();
        public ItemHeatSourceDefinition HeatSource =>
            heatSource ??= new ItemHeatSourceDefinition();
        public ItemCombustionDefinition Combustion =>
            combustion ??= new ItemCombustionDefinition();
        public IReadOnlyList<ItemColliderShapeDefinition> ColliderShapes =>
            colliderShapes ?? Array.Empty<ItemColliderShapeDefinition>();
        public bool HasAuthoredColliderShapes => ColliderShapes.Count > 0;

        public bool TryValidate(out string failure)
        {
            if (!Food.TryValidate(out string foodFailure))
            {
                failure = $"Item definition '{definitionId}' has invalid food data: {foodFailure}";
                return false;
            }

            if (!HeatSource.TryValidate(out string heatFailure))
            {
                failure = $"Item definition '{definitionId}' has invalid heat-source data: {heatFailure}";
                return false;
            }

            if (!Combustion.TryValidate(out string combustionFailure))
            {
                failure = $"Item definition '{definitionId}' has invalid combustion data: {combustionFailure}";
                return false;
            }

            if (!ItemDefinitionId.IsValid(definitionId) ||
                string.IsNullOrWhiteSpace(featureId) ||
                string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(category) ||
                string.IsNullOrWhiteSpace(replacementKey))
            {
                failure = "Item definition identity or presentation key is invalid.";
                return false;
            }

            if (!float.IsFinite(maximumContent) ||
                !float.IsFinite(initialContent) ||
                !float.IsFinite(useAmount) ||
                initialContent < 0f ||
                maximumContent < initialContent ||
                useAmount < 0f ||
                !float.IsFinite(massKilograms) ||
                massKilograms <= 0f ||
                !float.IsFinite(emptyContainerMassKilograms) ||
                emptyContainerMassKilograms < 0f ||
                emptyContainerMassKilograms > massKilograms ||
                !float.IsFinite(maximumCarryMassKilograms) ||
                maximumCarryMassKilograms <= 0f ||
                !float.IsFinite(hungerEffect) ||
                !float.IsFinite(thirstEffect) ||
                !float.IsFinite(stressEffect) ||
                !float.IsFinite(weightEffect) ||
                !float.IsFinite(intoxicationEffect) ||
                !float.IsFinite(urineEffect) ||
                !float.IsFinite(fatigueEffect) ||
                !float.IsFinite(dirtinessEffect) ||
                !IsFinitePositive(proxySize) ||
                initialChildCount < 0)
            {
                failure = $"Item definition '{definitionId}' has invalid physical or content limits.";
                return false;
            }

            if (initialChildCount > 0 &&
                !ItemDefinitionId.IsValid(childDefinitionId))
            {
                failure = $"Item definition '{definitionId}' has no valid child definition.";
                return false;
            }

            if (!string.IsNullOrEmpty(producedDefinitionId) &&
                !ItemDefinitionId.IsValid(producedDefinitionId))
            {
                failure = $"Item definition '{definitionId}' has an invalid produced definition.";
                return false;
            }

            bool hasInitialLiquid =
                !string.IsNullOrWhiteSpace(initialLiquidId);
            if (supportsLiquidTransfer &&
                ((initialContent > 0.0001f && !hasInitialLiquid) ||
                 (initialContent <= 0.0001f && hasInitialLiquid)) ||
                !supportsLiquidTransfer && hasInitialLiquid)
            {
                failure =
                    $"Item definition '{definitionId}' has inconsistent initial liquid state.";
                return false;
            }

            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemScalarDefinition state in ScalarStates)
            {
                if (state == null ||
                    !ItemStatePropertyId.IsValid(state.StateId) ||
                    !float.IsFinite(state.Minimum) ||
                    !float.IsFinite(state.Maximum) ||
                    !float.IsFinite(state.InitialValue) ||
                    state.Maximum < state.Minimum ||
                    state.InitialValue < state.Minimum ||
                    state.InitialValue > state.Maximum ||
                    !stateIds.Add(state.StateId))
                {
                    failure = $"Item definition '{definitionId}' has an invalid scalar state.";
                    return false;
                }
            }

            foreach (ItemFlagDefinition state in FlagStates)
            {
                if (state == null ||
                    !ItemStatePropertyId.IsValid(state.StateId) ||
                    !stateIds.Add(state.StateId))
                {
                    failure = $"Item definition '{definitionId}' has an invalid flag state.";
                    return false;
                }
            }

            ItemScalarDefinition combustionTime = Combustion.IsConfigured
                ? ScalarStates.FirstOrDefault(state => string.Equals(
                    state.StateId,
                    Combustion.BurnTimeStateId,
                    StringComparison.Ordinal))
                : null;
            if (Combustion.IsConfigured &&
                (combustionTime == null ||
                 combustionTime.Minimum > 0f ||
                 combustionTime.Maximum < Combustion.ActiveDurationSeconds ||
                 !FlagStates.Any(state => string.Equals(
                     state.StateId,
                     Combustion.WetStateId,
                     StringComparison.Ordinal)) ||
                 maximumContent <= Combustion.MinimumFuelToIgnite))
            {
                failure = $"Item definition '{definitionId}' is missing " +
                          "combustion-owned state or fuel capacity.";
                return false;
            }

            foreach (ItemColliderShapeDefinition shape in ColliderShapes)
            {
                if (shape == null)
                {
                    failure = $"Item definition '{definitionId}' has invalid " +
                              "collider data: shape is missing.";
                    return false;
                }

                if (!shape.TryValidate(out string shapeFailure))
                {
                    failure = $"Item definition '{definitionId}' has invalid " +
                              $"collider data: {shapeFailure}";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredDefinitionId,
            string configuredFeatureId,
            string configuredDisplayName,
            string configuredCategory,
            string configuredParentFeatureId,
            string configuredDonorEvidence,
            string configuredReplacementKey,
            bool isRequired,
            ItemCalibrationStatus configuredCalibration,
            ItemContentMeasure configuredMeasure,
            ItemPrimaryAction configuredAction,
            float configuredMaximumContent,
            float configuredInitialContent,
            float configuredUseAmount,
            float configuredMassKilograms,
            float configuredEmptyContainerMassKilograms,
            float configuredMaximumCarryMass,
            Vector3 configuredProxySize,
            bool configuredCanOpen,
            bool configuredStartsOpen,
            bool configuredRetainWhenEmpty,
            bool configuredSupportsLiquidTransfer,
            string configuredInitialLiquidId,
            bool configuredCriticalRecovery,
            int configuredInitialChildCount,
            string configuredChildDefinitionId,
            string configuredProducedDefinitionId,
            string configuredToolType,
            string[] configuredToolVariants,
            Vector4 needEffects,
            float configuredIntoxicationEffect,
            ItemScalarDefinition[] configuredScalarStates,
            ItemFlagDefinition[] configuredFlagStates)
        {
            definitionId = configuredDefinitionId ?? string.Empty;
            featureId = configuredFeatureId ?? string.Empty;
            displayName = configuredDisplayName ?? string.Empty;
            category = configuredCategory ?? string.Empty;
            parentFeatureId = configuredParentFeatureId ?? string.Empty;
            donorEvidence = configuredDonorEvidence ?? string.Empty;
            replacementKey = configuredReplacementKey ?? string.Empty;
            required = isRequired;
            calibrationStatus = configuredCalibration;
            contentMeasure = configuredMeasure;
            primaryAction = configuredAction;
            maximumContent = configuredMaximumContent;
            initialContent = configuredInitialContent;
            useAmount = configuredUseAmount;
            massKilograms = configuredMassKilograms;
            emptyContainerMassKilograms =
                configuredEmptyContainerMassKilograms;
            maximumCarryMassKilograms = configuredMaximumCarryMass;
            proxySize = configuredProxySize;
            canOpen = configuredCanOpen;
            startsOpen = configuredStartsOpen;
            retainWhenEmpty = configuredRetainWhenEmpty;
            supportsLiquidTransfer = configuredSupportsLiquidTransfer;
            initialLiquidId = configuredInitialLiquidId ?? string.Empty;
            criticalRecovery = configuredCriticalRecovery;
            initialChildCount = configuredInitialChildCount;
            childDefinitionId = configuredChildDefinitionId ?? string.Empty;
            producedDefinitionId = configuredProducedDefinitionId ?? string.Empty;
            toolType = configuredToolType ?? string.Empty;
            toolVariants = configuredToolVariants ?? Array.Empty<string>();
            hungerEffect = needEffects.x;
            thirstEffect = needEffects.y;
            stressEffect = needEffects.z;
            weightEffect = needEffects.w;
            intoxicationEffect = configuredIntoxicationEffect;
            scalarStates = configuredScalarStates ?? Array.Empty<ItemScalarDefinition>();
            flagStates = configuredFlagStates ?? Array.Empty<ItemFlagDefinition>();
        }

        public void ConfigureLifeEffectsForAuthoring(
            float configuredUrineEffect,
            float configuredFatigueEffect,
            float configuredDirtinessEffect)
        {
            urineEffect = configuredUrineEffect;
            fatigueEffect = configuredFatigueEffect;
            dirtinessEffect = configuredDirtinessEffect;
        }

        public void ConfigureFoodForAuthoring(
            ItemFoodDefinition configuredFood,
            ItemHeatSourceDefinition configuredHeatSource)
        {
            food = configuredFood ?? new ItemFoodDefinition();
            heatSource = configuredHeatSource ??
                new ItemHeatSourceDefinition();
        }

        public void ConfigureCombustionForAuthoring(
            ItemCombustionDefinition configuredCombustion)
        {
            combustion = configuredCombustion ??
                new ItemCombustionDefinition();
        }

        public void ConfigurePhysicalShapesForAuthoring(
            ItemColliderShapeDefinition[] configuredShapes)
        {
            colliderShapes = configuredShapes ??
                Array.Empty<ItemColliderShapeDefinition>();
        }
#endif

        private static bool IsFinitePositive(Vector3 value) =>
            float.IsFinite(value.x) && value.x > 0f &&
            float.IsFinite(value.y) && value.y > 0f &&
            float.IsFinite(value.z) && value.z > 0f;
    }

    public static class ItemDefinitionId
    {
        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96 ||
                !value.StartsWith("item.", StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') &&
                    character != '.' && character != '-')
                {
                    return false;
                }
            }

            return true;
        }
    }

    public static class ItemStatePropertyId
    {
        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') &&
                    character != '.' && character != '-')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
