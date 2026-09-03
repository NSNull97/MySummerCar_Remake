using System;
using UnityEngine;

namespace MSC.Items
{
    /// <summary>
    /// Immutable, authored fuel/combustion rules. Runtime state stays in the
    /// ordinary item DTO: content is fuel, isEnabled is heat, and the authored
    /// scalar stores remaining visible-flame time.
    /// </summary>
    [Serializable]
    public sealed class ItemCombustionDefinition
    {
        [SerializeField] private bool configured;
        [SerializeField] private string acceptedFuelDefinitionId = string.Empty;
        [SerializeField] private string burnTimeStateId = string.Empty;
        [SerializeField] private string wetStateId = string.Empty;
        [SerializeField, Min(0f)] private float minimumFuelToIgnite;
        [SerializeField, Min(0f)] private float extinguishFuelThreshold;
        [SerializeField, Min(0.01f)] private float activeDurationSeconds = 1f;
        [SerializeField, Min(0.0001f)] private float activeFuelPerSecond = 0.1f;
        [SerializeField, Min(0.0001f)] private float emberFuelPerSecond = 0.04f;
        [SerializeField, Min(0.0001f)] private float fuelPourRatePerSecond = 1f;
        [SerializeField, Range(0f, 180f)] private float minimumFuelPourTiltDegrees = 80f;
        [SerializeField, Min(0.01f)] private float firePresentationScale = 1f;
        [SerializeField, Min(0.01f)] private float fireEmissionMultiplier = 1f;
        [SerializeField, Min(0f)] private float fireLightIntensity = 720f;
        [SerializeField, Min(0f)] private float fireLightRange = 4.2f;

        public bool IsConfigured => configured;
        public string AcceptedFuelDefinitionId => acceptedFuelDefinitionId;
        public string BurnTimeStateId => burnTimeStateId;
        public string WetStateId => wetStateId;
        public float MinimumFuelToIgnite => minimumFuelToIgnite;
        public float ExtinguishFuelThreshold => extinguishFuelThreshold;
        public float ActiveDurationSeconds => activeDurationSeconds;
        public float ActiveFuelPerSecond => activeFuelPerSecond;
        public float EmberFuelPerSecond => emberFuelPerSecond;
        public float FuelPourRatePerSecond => fuelPourRatePerSecond;
        public float MinimumFuelPourTiltDegrees =>
            minimumFuelPourTiltDegrees;
        public float FirePresentationScale => firePresentationScale;
        public float FireEmissionMultiplier => fireEmissionMultiplier;
        public float FireLightIntensity => fireLightIntensity;
        public float FireLightRange => fireLightRange;

        public bool TryValidate(out string failure)
        {
            if (!configured)
            {
                failure = string.Empty;
                return true;
            }

            if (!ItemDefinitionId.IsValid(acceptedFuelDefinitionId) ||
                !ItemStatePropertyId.IsValid(burnTimeStateId) ||
                !ItemStatePropertyId.IsValid(wetStateId) ||
                !float.IsFinite(minimumFuelToIgnite) ||
                !float.IsFinite(extinguishFuelThreshold) ||
                !float.IsFinite(activeDurationSeconds) ||
                !float.IsFinite(activeFuelPerSecond) ||
                !float.IsFinite(emberFuelPerSecond) ||
                !float.IsFinite(fuelPourRatePerSecond) ||
                !float.IsFinite(minimumFuelPourTiltDegrees) ||
                !float.IsFinite(firePresentationScale) ||
                !float.IsFinite(fireEmissionMultiplier) ||
                !float.IsFinite(fireLightIntensity) ||
                !float.IsFinite(fireLightRange) ||
                minimumFuelToIgnite < 0f ||
                extinguishFuelThreshold < 0f ||
                extinguishFuelThreshold >= minimumFuelToIgnite ||
                activeDurationSeconds <= 0f ||
                activeFuelPerSecond <= 0f ||
                emberFuelPerSecond <= 0f ||
                fuelPourRatePerSecond <= 0f ||
                minimumFuelPourTiltDegrees < 0f ||
                minimumFuelPourTiltDegrees > 180f ||
                firePresentationScale <= 0f ||
                fireEmissionMultiplier <= 0f ||
                fireLightIntensity < 0f ||
                fireLightRange < 0f)
            {
                failure = "Configured combustion values are invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredFuelDefinitionId,
            string configuredBurnTimeStateId,
            string configuredWetStateId,
            float configuredMinimumFuelToIgnite,
            float configuredExtinguishFuelThreshold,
            float configuredActiveDurationSeconds,
            float configuredActiveFuelPerSecond,
            float configuredEmberFuelPerSecond,
            float configuredFuelPourRatePerSecond,
            float configuredMinimumFuelPourTiltDegrees,
            float configuredFirePresentationScale = 1f,
            float configuredFireEmissionMultiplier = 1f,
            float configuredFireLightIntensity = 720f,
            float configuredFireLightRange = 4.2f)
        {
            configured = true;
            acceptedFuelDefinitionId = configuredFuelDefinitionId ??
                string.Empty;
            burnTimeStateId = configuredBurnTimeStateId ?? string.Empty;
            wetStateId = configuredWetStateId ?? string.Empty;
            minimumFuelToIgnite = configuredMinimumFuelToIgnite;
            extinguishFuelThreshold = configuredExtinguishFuelThreshold;
            activeDurationSeconds = configuredActiveDurationSeconds;
            activeFuelPerSecond = configuredActiveFuelPerSecond;
            emberFuelPerSecond = configuredEmberFuelPerSecond;
            fuelPourRatePerSecond = configuredFuelPourRatePerSecond;
            minimumFuelPourTiltDegrees =
                configuredMinimumFuelPourTiltDegrees;
            firePresentationScale = configuredFirePresentationScale;
            fireEmissionMultiplier = configuredFireEmissionMultiplier;
            fireLightIntensity = configuredFireLightIntensity;
            fireLightRange = configuredFireLightRange;
        }
#endif
    }
}
