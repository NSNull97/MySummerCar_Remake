using System;
using System.Collections.Generic;
using MSC.World.Lighting;
using UnityEngine;

namespace MSC.Lighting.Production
{
    [Serializable]
    public sealed class WorldLightingFixtureBinding
    {
        [SerializeField] private string worldLightId = string.Empty;
        [SerializeField] private LightFixtureCategory category;
        [SerializeField] private string powerSourceId = "source.grid.main";
        [SerializeField] private string circuitId = string.Empty;
        [SerializeField] private string switchId = string.Empty;
        [SerializeField] private string zoneId = "zone.exterior";
        [SerializeField] private string businessId = string.Empty;

        public string WorldLightId => worldLightId;
        public LightFixtureCategory Category => category;
        public string PowerSourceId => powerSourceId;
        public string CircuitId => circuitId;
        public string SwitchId => switchId;
        public string ZoneId => zoneId;
        public string BusinessId => businessId;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredWorldLightId,
            LightFixtureCategory configuredCategory,
            string configuredPowerSourceId,
            string configuredCircuitId,
            string configuredSwitchId,
            string configuredZoneId,
            string configuredBusinessId)
        {
            worldLightId = configuredWorldLightId ?? string.Empty;
            category = configuredCategory;
            powerSourceId = configuredPowerSourceId ?? string.Empty;
            circuitId = configuredCircuitId ?? string.Empty;
            switchId = configuredSwitchId ?? string.Empty;
            zoneId = configuredZoneId ?? string.Empty;
            businessId = configuredBusinessId ?? string.Empty;
        }
#endif
    }

    [CreateAssetMenu(
        menuName = "MSC/Lighting/World Fixture Binding Catalog",
        fileName = "WorldLightingBindingCatalog")]
    public sealed class WorldLightingBindingCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId =
            "lighting.world-bindings.phase1.v1";
        [SerializeField] private WorldLightingProbeCatalog sourceCatalog;
        [SerializeField] private List<WorldLightingFixtureBinding> bindings =
            new();

        public string CatalogId => catalogId;
        public WorldLightingProbeCatalog SourceCatalog => sourceCatalog;
        public IReadOnlyList<WorldLightingFixtureBinding> Bindings => bindings;

        public bool TryGet(
            string worldLightId,
            out WorldLightingFixtureBinding binding)
        {
            for (int index = 0; index < bindings.Count; index++)
            {
                WorldLightingFixtureBinding candidate = bindings[index];
                if (candidate != null && string.Equals(
                        candidate.WorldLightId,
                        worldLightId,
                        StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
            }

            binding = null;
            return false;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(catalogId) || sourceCatalog == null)
            {
                throw new InvalidOperationException(
                    "World lighting binding catalog is incomplete.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < bindings.Count; index++)
            {
                WorldLightingFixtureBinding binding = bindings[index];
                if (binding == null ||
                    string.IsNullOrWhiteSpace(binding.WorldLightId) ||
                    string.IsNullOrWhiteSpace(binding.PowerSourceId) ||
                    string.IsNullOrWhiteSpace(binding.CircuitId) ||
                    string.IsNullOrWhiteSpace(binding.ZoneId) ||
                    !ids.Add(binding.WorldLightId))
                {
                    throw new InvalidOperationException(
                        $"World lighting binding {index} is invalid.");
                }
            }

            if (bindings.Count != sourceCatalog.Lights.Count)
            {
                throw new InvalidOperationException(
                    "Every generated world light requires an explicit binding.");
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            WorldLightingProbeCatalog configuredSourceCatalog,
            List<WorldLightingFixtureBinding> configuredBindings)
        {
            catalogId = id ?? string.Empty;
            sourceCatalog = configuredSourceCatalog;
            bindings = configuredBindings ??
                new List<WorldLightingFixtureBinding>();
            Validate();
        }
#endif
    }
}
