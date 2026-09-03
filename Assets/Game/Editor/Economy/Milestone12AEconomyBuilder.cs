using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MSC.Bootstrap;
using MSC.Economy;
using MSC.LegacyImport.Editor.GameplayPresentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.Economy
{
    public static class Milestone12AEconomyBuilder
    {
        private const string CatalogPath =
            "Assets/Game/Economy/Content/Phase1/EconomyPriceCatalog.asset";
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string BuildReportPath =
            "Docs/Milestones/MILESTONE_12A_E1_BUILD_REPORT.json";

        private static readonly ExpandedShopPriceSeed[] ExpandedShopPrices =
        {
            new("buttermilk", "Buttermilk", 525),
            new("orange-juice", "Orange juice", 749),
            new("bug-spray", "Bug spray", 6_995),
            new("laundry-detergent", "Laundry detergent", 1_995),
            new("sponge", "Sponge", 729),
            new("shampoo", "Shampoo", 695),
            new("hand-soap", "Hand soap", 549),
            new("dish-soap", "Dish soap", 949),
            new("soap", "Soap", 875),
            new("wheat-flour", "Wheat flour", 649),
            new("rye-flour", "Rye flour", 699),
            new("mustard", "Mustard", 795),
            new("ketchup", "Ketchup", 895),
            new("meat-soup", "Meat soup", 979),
            new("pea-soup", "Pea soup", 849),
            new("canned-meatballs", "Canned meatballs", 1_099),
            new("sausage", "Sausage", 1_579),
            new("fishstick-box", "Fishstick box", 995),
            new("can-opener", "Can opener", 7_900),
        };

        [MenuItem("Tools/My Summer Car/Phase 1/Build Economy Foundation (12A-E1)")]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog(
                "Milestone 12A-E1",
                "The money, price and transaction foundation was rebuilt.",
                "OK");
        }

        public static void BuildFromBatch() => Build();

        public static void Build()
        {
            LockedEconomyEvidence evidence =
                Phase1EconomyEvidence.LoadLockedEvidence();
            if (evidence.StorePriceKeys.Length !=
                evidence.StoreBasePricesMarkka.Length)
            {
                throw new InvalidOperationException(
                    "Locked economy key/value counts differ.");
            }

            var storeScope = new EconomyPriceScopeDefinition();
            storeScope.ConfigureForAuthoring(
                "scope.store.teimo",
                EconomyPricePolicy.WeeklyAdditiveInflation,
                checked((int)decimal.Round(
                    evidence.StoreInflationRate * 10_000m,
                    0,
                    MidpointRounding.AwayFromZero)),
                DayOfWeek.Thursday);

            var prices = new List<EconomyPriceDefinition>(
                evidence.StorePriceKeys.Length + ExpandedShopPrices.Length);
            for (int index = 0;
                 index < evidence.StorePriceKeys.Length;
                 index++)
            {
                var definition = new EconomyPriceDefinition();
                definition.ConfigureForAuthoring(
                    "price.store." + ToStableSlug(
                        evidence.StorePriceKeys[index]),
                    storeScope.ScopeId,
                    evidence.StorePriceKeys[index],
                    ToMinorUnits(evidence.StoreBasePricesMarkka[index]));
                prices.Add(definition);
            }

            foreach (ExpandedShopPriceSeed seed in ExpandedShopPrices)
            {
                var definition = new EconomyPriceDefinition();
                definition.ConfigureForAuthoring(
                    "price.store.expanded-shop-" + seed.Slug,
                    storeScope.ScopeId,
                    "ExpandedShop:" + seed.DisplayName,
                    seed.PriceMinorUnits);
                prices.Add(definition);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath) ??
                "Assets/Game/Economy/Content/Phase1");
            EconomyPriceCatalog catalog = AssetDatabase.LoadAssetAtPath<
                EconomyPriceCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EconomyPriceCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.name = "Phase1EconomyPriceCatalog";
            catalog.ConfigureForAuthoring(
                "catalog.economy.phase1.12a-e1.v1",
                Phase1EconomyEvidence.LockedSceneSha256,
                Phase1EconomyEvidence.LockedGlobalsSha256,
                ToMinorUnits(evidence.InitialBalanceMarkka),
                new[] { storeScope },
                prices.ToArray());
            if (!catalog.TryValidate(out string failure))
            {
                throw new InvalidOperationException(failure);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            BindBootstrap(catalog);
            WriteBuildReport(catalog, evidence);
            AssetDatabase.Refresh();
            Debug.Log(
                $"Milestone 12A-E1 built: {catalog.InitialBalanceMinorUnits / 100m:F2} MK, " +
                $"{evidence.StorePriceKeys.Length} locked donor prices plus " +
                $"{ExpandedShopPrices.Length} Expanded Shop prices and " +
                $"{storeScope.InflationBasisPoints / 100m:F2}% Thursday inflation.");
        }

        private static void BindBootstrap(EconomyPriceCatalog catalog)
        {
            Scene scene = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            ProductionWorldStreamingInstaller installer =
                UnityEngine.Object.FindFirstObjectByType<
                    ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            if (installer == null)
            {
                throw new InvalidOperationException(
                    "Bootstrap has no ProductionWorldStreamingInstaller.");
            }

            installer.ConfigureEconomyForAuthoring(catalog);
            EditorUtility.SetDirty(installer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WriteBuildReport(
            EconomyPriceCatalog catalog,
            LockedEconomyEvidence evidence)
        {
            string json =
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"milestoneId\": \"12A-E1\",\n" +
                "  \"classification\": \"Reimplemented\",\n" +
                $"  \"sourceSceneSha256\": \"{catalog.DonorSceneSha256}\",\n" +
                $"  \"sourceGlobalsSha256\": \"{catalog.DonorGlobalsSha256}\",\n" +
                $"  \"initialBalanceMinorUnits\": {catalog.InitialBalanceMinorUnits},\n" +
                $"  \"lockedStorePriceCount\": {evidence.StorePriceKeys.Length},\n" +
                $"  \"expandedShopPriceCount\": {ExpandedShopPrices.Length},\n" +
                $"  \"storePriceCount\": {catalog.Prices.Count},\n" +
                $"  \"storeInflationRate\": {evidence.StoreInflationRate.ToString(CultureInfo.InvariantCulture)},\n" +
                $"  \"storeRestockDay\": {evidence.StoreRestockDay},\n" +
                "  \"donorFsmRuntimeExcluded\": true\n" +
                "}\n";
            File.WriteAllText(BuildReportPath, json, new UTF8Encoding(false));
        }

        private static long ToMinorUnits(decimal markka) =>
            checked((long)decimal.Round(
                markka * 100m,
                0,
                MidpointRounding.AwayFromZero));

        private static string ToStableSlug(string source)
        {
            var builder = new StringBuilder(source.Length + 8);
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                if (char.IsUpper(character) && index > 0 &&
                    (char.IsLower(source[index - 1]) ||
                     char.IsDigit(source[index - 1])))
                {
                    builder.Append('-');
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
                else if (builder.Length > 0 &&
                         builder[builder.Length - 1] != '-')
                {
                    builder.Append('-');
                }
            }

            return builder.ToString().Trim('-');
        }

        private readonly struct ExpandedShopPriceSeed
        {
            public ExpandedShopPriceSeed(
                string slug,
                string displayName,
                long priceMinorUnits)
            {
                Slug = slug;
                DisplayName = displayName;
                PriceMinorUnits = priceMinorUnits;
            }

            public string Slug { get; }
            public string DisplayName { get; }
            public long PriceMinorUnits { get; }
        }
    }
}
