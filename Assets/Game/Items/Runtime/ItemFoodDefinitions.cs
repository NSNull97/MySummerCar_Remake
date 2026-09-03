using System;
using UnityEngine;

namespace MSC.Items
{
    public enum FoodCookState
    {
        Raw = 0,
        Cooked = 1,
        Burned = 2,
    }

    public enum ItemConsumptionPresentation
    {
        None = 0,
        Drink = 1,
    }

    /// <summary>
    /// Authored needs result for a particular food state. Definitions may
    /// leave this disabled to retain the ordinary item-use effects.
    /// </summary>
    [Serializable]
    public sealed class FoodEffectDefinition
    {
        [SerializeField] private bool overridesBaseEffects;
        [SerializeField] private float hunger;
        [SerializeField] private float thirst;
        [SerializeField] private float stress;
        [SerializeField] private float weight;
        [SerializeField] private float intoxication;
        [SerializeField] private float urine;
        [SerializeField] private float fatigue;
        [SerializeField] private float dirtiness;

        public bool OverridesBaseEffects => overridesBaseEffects;

        public bool TryValidate(out string failure)
        {
            if (!float.IsFinite(hunger) ||
                !float.IsFinite(thirst) ||
                !float.IsFinite(stress) ||
                !float.IsFinite(weight) ||
                !float.IsFinite(intoxication) ||
                !float.IsFinite(urine) ||
                !float.IsFinite(fatigue) ||
                !float.IsFinite(dirtiness))
            {
                failure = "Food effects contain a non-finite value.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public ItemUseEffectDelta Resolve(
            in ItemUseEffectDelta baseEffects,
            float scale)
        {
            ItemUseEffectDelta source = overridesBaseEffects
                ? new ItemUseEffectDelta(
                    hunger,
                    thirst,
                    stress,
                    weight,
                    intoxication,
                    urine,
                    fatigue,
                    dirtiness)
                : baseEffects;
            return source.Scaled(scale);
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            bool configuredOverride,
            Vector4 primaryEffects,
            float configuredIntoxication = 0f,
            float configuredUrine = 0f,
            float configuredFatigue = 0f,
            float configuredDirtiness = 0f)
        {
            overridesBaseEffects = configuredOverride;
            hunger = primaryEffects.x;
            thirst = primaryEffects.y;
            stress = primaryEffects.z;
            weight = primaryEffects.w;
            intoxication = configuredIntoxication;
            urine = configuredUrine;
            fatigue = configuredFatigue;
            dirtiness = configuredDirtiness;
        }
#endif
    }

    /// <summary>
    /// Immutable, data-driven food behavior. Rates are condition points per
    /// in-game minute, matching the normalized donor SpoilRate/FridgeRate
    /// evidence rather than real wall-clock time.
    /// </summary>
    [Serializable]
    public sealed class ItemFoodDefinition
    {
        [SerializeField] private bool edible;
        [SerializeField] private bool perishable;
        [SerializeField] private bool cookable;
        [SerializeField] private bool consumeWhole;
        [SerializeField] private ItemConsumptionPresentation consumptionPresentation;
        [SerializeField, Min(0f)] private float consumptionDurationSeconds;
        [SerializeField, Min(0f)] private float ambientDecayPerGameMinute;
        [SerializeField, Min(0f)] private float refrigeratedDecayPerGameMinute;
        [SerializeField, Min(0f)] private float cookedAfterSeconds;
        [SerializeField, Min(0f)] private float burnedAfterAdditionalSeconds;
        [SerializeField] private Color rawTint = Color.white;
        [SerializeField] private Color cookedTint = new Color(0.72f, 0.42f, 0.2f, 1f);
        [SerializeField] private Color burnedTint = new Color(0.12f, 0.08f, 0.05f, 1f);
        [SerializeField] private Color spoiledTint = new Color(0.42f, 0.62f, 0.25f, 1f);
        [SerializeField] private FoodEffectDefinition cookedEffects = new FoodEffectDefinition();
        [SerializeField] private FoodEffectDefinition burnedEffects = new FoodEffectDefinition();
        [SerializeField] private FoodEffectDefinition spoiledEffects = new FoodEffectDefinition();

        public bool Edible => edible;
        public bool Perishable => perishable;
        public bool Cookable => cookable;
        public bool ConsumeWhole => consumeWhole;
        public ItemConsumptionPresentation ConsumptionPresentation =>
            consumptionPresentation;
        public float ConsumptionDurationSeconds => consumptionDurationSeconds;
        public float AmbientDecayPerGameMinute => ambientDecayPerGameMinute;
        public float RefrigeratedDecayPerGameMinute => refrigeratedDecayPerGameMinute;
        public float CookedAfterSeconds => cookedAfterSeconds;
        public float BurnedAfterAdditionalSeconds => burnedAfterAdditionalSeconds;
        public float MaximumCookingSeconds => cookedAfterSeconds + burnedAfterAdditionalSeconds;

        public bool IsConfigured => edible || perishable || cookable;

        public bool TryValidate(out string failure)
        {
            failure = string.Empty;
            if (!float.IsFinite(consumptionDurationSeconds) ||
                consumptionDurationSeconds < 0f ||
                !float.IsFinite(ambientDecayPerGameMinute) ||
                ambientDecayPerGameMinute < 0f ||
                !float.IsFinite(refrigeratedDecayPerGameMinute) ||
                refrigeratedDecayPerGameMinute < 0f ||
                !float.IsFinite(cookedAfterSeconds) ||
                cookedAfterSeconds < 0f ||
                !float.IsFinite(burnedAfterAdditionalSeconds) ||
                burnedAfterAdditionalSeconds < 0f ||
                !Enum.IsDefined(typeof(ItemConsumptionPresentation),
                    consumptionPresentation) ||
                (consumptionPresentation != ItemConsumptionPresentation.None &&
                 !edible) ||
                perishable && ambientDecayPerGameMinute <= 0f ||
                perishable && refrigeratedDecayPerGameMinute >
                    ambientDecayPerGameMinute ||
                cookable && (!edible || cookedAfterSeconds <= 0f ||
                    burnedAfterAdditionalSeconds <= 0f) ||
                !IsFinite(rawTint) || !IsFinite(cookedTint) ||
                !IsFinite(burnedTint) || !IsFinite(spoiledTint) ||
                cookedEffects == null || burnedEffects == null ||
                spoiledEffects == null ||
                !cookedEffects.TryValidate(out failure) ||
                !burnedEffects.TryValidate(out failure) ||
                !spoiledEffects.TryValidate(out failure))
            {
                failure = string.IsNullOrWhiteSpace(failure)
                    ? "Food definition contains invalid rates, thresholds, tints, or effects."
                    : failure;
                return false;
            }

            return true;
        }

        public FoodCookState ResolveCookState(float cookingSeconds)
        {
            if (!cookable || cookingSeconds < cookedAfterSeconds)
            {
                return FoodCookState.Raw;
            }

            return cookingSeconds < MaximumCookingSeconds
                ? FoodCookState.Cooked
                : FoodCookState.Burned;
        }

        public Color ResolveTint(float freshness, float cookingSeconds)
        {
            if (perishable && freshness <= 0.0001f)
            {
                return spoiledTint;
            }

            return ResolveCookState(cookingSeconds) switch
            {
                FoodCookState.Cooked => cookedTint,
                FoodCookState.Burned => burnedTint,
                _ => rawTint,
            };
        }

        public ItemUseEffectDelta ResolveEffects(
            in ItemUseEffectDelta baseEffects,
            float freshness,
            float cookingSeconds,
            float scale)
        {
            if (perishable && freshness <= 0.0001f)
            {
                return spoiledEffects.Resolve(baseEffects, scale);
            }

            return ResolveCookState(cookingSeconds) switch
            {
                FoodCookState.Cooked => cookedEffects.Resolve(baseEffects, scale),
                FoodCookState.Burned => burnedEffects.Resolve(baseEffects, scale),
                _ => baseEffects.Scaled(scale),
            };
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            bool configuredEdible,
            bool configuredPerishable,
            bool configuredCookable,
            bool configuredConsumeWhole,
            float configuredConsumptionDurationSeconds,
            float configuredAmbientDecayPerGameMinute,
            float configuredRefrigeratedDecayPerGameMinute,
            float configuredCookedAfterSeconds,
            float configuredBurnedAfterAdditionalSeconds,
            FoodEffectDefinition configuredCookedEffects,
            FoodEffectDefinition configuredBurnedEffects,
            FoodEffectDefinition configuredSpoiledEffects,
            Color configuredRawTint,
            Color configuredCookedTint,
            Color configuredBurnedTint,
            Color configuredSpoiledTint,
            ItemConsumptionPresentation configuredConsumptionPresentation =
                ItemConsumptionPresentation.None)
        {
            edible = configuredEdible;
            perishable = configuredPerishable;
            cookable = configuredCookable;
            consumeWhole = configuredConsumeWhole;
            consumptionPresentation = configuredConsumptionPresentation;
            consumptionDurationSeconds = configuredConsumptionDurationSeconds;
            ambientDecayPerGameMinute = configuredAmbientDecayPerGameMinute;
            refrigeratedDecayPerGameMinute = configuredRefrigeratedDecayPerGameMinute;
            cookedAfterSeconds = configuredCookedAfterSeconds;
            burnedAfterAdditionalSeconds = configuredBurnedAfterAdditionalSeconds;
            cookedEffects = configuredCookedEffects ?? new FoodEffectDefinition();
            burnedEffects = configuredBurnedEffects ?? new FoodEffectDefinition();
            spoiledEffects = configuredSpoiledEffects ?? new FoodEffectDefinition();
            rawTint = configuredRawTint;
            cookedTint = configuredCookedTint;
            burnedTint = configuredBurnedTint;
            spoiledTint = configuredSpoiledTint;
        }
#endif

        private static bool IsFinite(Color value) =>
            float.IsFinite(value.r) && float.IsFinite(value.g) &&
            float.IsFinite(value.b) && float.IsFinite(value.a);
    }

    [Serializable]
    public sealed class ItemHeatSourceDefinition
    {
        [SerializeField] private bool providesCookingHeat;
        [SerializeField] private bool requiresItemEnabled = true;
        [SerializeField, Min(0.01f)] private float cookingRate = 1f;
        [SerializeField] private Vector3 localCenter = Vector3.up * 0.2f;
        [SerializeField] private Vector3 localSize = new Vector3(0.45f, 0.16f, 0.45f);
        [SerializeField] private Vector3 localUp = Vector3.up;

        public bool ProvidesCookingHeat => providesCookingHeat;
        public bool RequiresItemEnabled => requiresItemEnabled;
        public float CookingRate => cookingRate;
        public Vector3 LocalCenter => localCenter;
        public Vector3 LocalSize => localSize;
        public Vector3 LocalUp => localUp.sqrMagnitude > 0.000001f
            ? localUp.normalized
            : Vector3.up;

        public bool TryValidate(out string failure)
        {
            if (!float.IsFinite(cookingRate) || cookingRate <= 0f ||
                !IsFinite(localCenter) || !IsFinitePositive(localSize) ||
                !IsFinite(localUp) || localUp.sqrMagnitude <= 0.000001f)
            {
                failure = "Item heat-source definition is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            bool configuredProvidesHeat,
            bool configuredRequiresEnabled,
            float configuredCookingRate,
            Vector3 configuredLocalCenter,
            Vector3 configuredLocalSize,
            Vector3 configuredLocalUp = default)
        {
            providesCookingHeat = configuredProvidesHeat;
            requiresItemEnabled = configuredRequiresEnabled;
            cookingRate = configuredCookingRate;
            localCenter = configuredLocalCenter;
            localSize = configuredLocalSize;
            localUp = configuredLocalUp.sqrMagnitude > 0.000001f
                ? configuredLocalUp.normalized
                : Vector3.up;
        }
#endif

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinitePositive(Vector3 value) =>
            IsFinite(value) && value.x > 0f && value.y > 0f && value.z > 0f;
    }

    public interface IItemFoodEnvironment
    {
        bool IsRefrigerated(WorldItemInstance item);

        bool IsRefrigerated(Vector3 worldPosition);
    }

    public interface IItemHeatSource
    {
        bool IsHeating { get; }
        float CookingRate { get; }
        Bounds WorldBounds { get; }
    }
}
