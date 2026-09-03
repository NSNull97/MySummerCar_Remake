using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Economy
{
    public enum EconomyPricePolicy
    {
        Fixed = 0,
        WeeklyAdditiveInflation = 1,
    }

    [Serializable]
    public sealed class EconomyPriceScopeDefinition
    {
        [SerializeField] private string scopeId = string.Empty;
        [SerializeField] private EconomyPricePolicy policy;
        [SerializeField, Min(0)] private int inflationBasisPoints;
        [SerializeField, Range(0, 6)] private int restockDayOfWeek;

        public string ScopeId => scopeId;
        public EconomyPricePolicy Policy => policy;
        public int InflationBasisPoints => inflationBasisPoints;
        public DayOfWeek RestockDayOfWeek => (DayOfWeek)restockDayOfWeek;

        public void ConfigureForAuthoring(
            string configuredScopeId,
            EconomyPricePolicy configuredPolicy,
            int configuredInflationBasisPoints,
            DayOfWeek configuredRestockDay)
        {
            scopeId = configuredScopeId ?? string.Empty;
            policy = configuredPolicy;
            inflationBasisPoints = configuredInflationBasisPoints;
            restockDayOfWeek = (int)configuredRestockDay;
        }
    }

    [Serializable]
    public sealed class EconomyPriceDefinition
    {
        [SerializeField] private string priceId = string.Empty;
        [SerializeField] private string scopeId = string.Empty;
        [SerializeField] private string donorKey = string.Empty;
        [SerializeField, Min(1)] private long baseMinorUnits = 1;

        public string PriceId => priceId;
        public string ScopeId => scopeId;
        public string DonorKey => donorKey;
        public long BaseMinorUnits => baseMinorUnits;

        public void ConfigureForAuthoring(
            string configuredPriceId,
            string configuredScopeId,
            string configuredDonorKey,
            long configuredBaseMinorUnits)
        {
            priceId = configuredPriceId ?? string.Empty;
            scopeId = configuredScopeId ?? string.Empty;
            donorKey = configuredDonorKey ?? string.Empty;
            baseMinorUnits = configuredBaseMinorUnits;
        }
    }

    [CreateAssetMenu(
        fileName = "EconomyPriceCatalog",
        menuName = "MSC/Economy/Price Catalog")]
    public sealed class EconomyPriceCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId = string.Empty;
        [SerializeField] private string donorSceneSha256 = string.Empty;
        [SerializeField] private string donorGlobalsSha256 = string.Empty;
        [SerializeField, Min(0)] private long initialBalanceMinorUnits;
        [SerializeField] private EconomyPriceScopeDefinition[] scopes =
            Array.Empty<EconomyPriceScopeDefinition>();
        [SerializeField] private EconomyPriceDefinition[] prices =
            Array.Empty<EconomyPriceDefinition>();

        private Dictionary<string, EconomyPriceScopeDefinition> scopesById;
        private Dictionary<string, EconomyPriceDefinition> pricesById;

        public string CatalogId => catalogId;
        public string DonorSceneSha256 => donorSceneSha256;
        public string DonorGlobalsSha256 => donorGlobalsSha256;
        public long InitialBalanceMinorUnits => initialBalanceMinorUnits;
        public IReadOnlyList<EconomyPriceScopeDefinition> Scopes => scopes;
        public IReadOnlyList<EconomyPriceDefinition> Prices => prices;

        public void ConfigureForAuthoring(
            string configuredCatalogId,
            string configuredDonorSceneSha256,
            string configuredDonorGlobalsSha256,
            long configuredInitialBalanceMinorUnits,
            EconomyPriceScopeDefinition[] configuredScopes,
            EconomyPriceDefinition[] configuredPrices)
        {
            catalogId = configuredCatalogId ?? string.Empty;
            donorSceneSha256 = configuredDonorSceneSha256 ?? string.Empty;
            donorGlobalsSha256 = configuredDonorGlobalsSha256 ?? string.Empty;
            initialBalanceMinorUnits = configuredInitialBalanceMinorUnits;
            scopes = configuredScopes ?? Array.Empty<EconomyPriceScopeDefinition>();
            prices = configuredPrices ?? Array.Empty<EconomyPriceDefinition>();
            scopesById = null;
            pricesById = null;
        }

        public bool TryGetScope(
            string scopeId,
            out EconomyPriceScopeDefinition definition)
        {
            EnsureIndices();
            return scopesById.TryGetValue(scopeId ?? string.Empty, out definition);
        }

        public bool TryGetPrice(
            string priceId,
            out EconomyPriceDefinition definition)
        {
            EnsureIndices();
            return pricesById.TryGetValue(priceId ?? string.Empty, out definition);
        }

        public bool TryValidate(out string failure)
        {
            if (!StableIdRules.IsCanonical(catalogId) ||
                donorSceneSha256.Length != 64 ||
                donorGlobalsSha256.Length != 64 ||
                initialBalanceMinorUnits < 0)
            {
                failure = "Economy catalog metadata is invalid.";
                return false;
            }

            var knownScopes = new HashSet<string>(StringComparer.Ordinal);
            foreach (EconomyPriceScopeDefinition scope in
                     scopes ?? Array.Empty<EconomyPriceScopeDefinition>())
            {
                if (scope == null ||
                    !StableIdRules.IsCanonical(scope.ScopeId) ||
                    !Enum.IsDefined(typeof(EconomyPricePolicy), scope.Policy) ||
                    scope.InflationBasisPoints < 0 ||
                    scope.InflationBasisPoints > 10_000 ||
                    !knownScopes.Add(scope.ScopeId))
                {
                    failure = "Economy price scope metadata is invalid or duplicated.";
                    return false;
                }

                if (scope.Policy == EconomyPricePolicy.Fixed &&
                    scope.InflationBasisPoints != 0)
                {
                    failure = $"Fixed price scope '{scope.ScopeId}' has inflation.";
                    return false;
                }
            }

            var knownPrices = new HashSet<string>(StringComparer.Ordinal);
            foreach (EconomyPriceDefinition price in
                     prices ?? Array.Empty<EconomyPriceDefinition>())
            {
                if (price == null ||
                    !StableIdRules.IsCanonical(price.PriceId) ||
                    !knownScopes.Contains(price.ScopeId) ||
                    string.IsNullOrWhiteSpace(price.DonorKey) ||
                    price.BaseMinorUnits <= 0 ||
                    !knownPrices.Add(price.PriceId))
                {
                    failure = "Economy price metadata is invalid or duplicated.";
                    return false;
                }
            }

            if (knownScopes.Count == 0 || knownPrices.Count == 0)
            {
                failure = "Economy catalog must contain scopes and prices.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private void EnsureIndices()
        {
            if (scopesById != null && pricesById != null)
            {
                return;
            }

            scopesById = (scopes ?? Array.Empty<EconomyPriceScopeDefinition>())
                .Where(value => value != null)
                .ToDictionary(value => value.ScopeId, StringComparer.Ordinal);
            pricesById = (prices ?? Array.Empty<EconomyPriceDefinition>())
                .Where(value => value != null)
                .ToDictionary(value => value.PriceId, StringComparer.Ordinal);
        }
    }

    internal static class StableIdRules
    {
        public static bool IsCanonical(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') &&
                    character != '.' && character != '-' && character != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
