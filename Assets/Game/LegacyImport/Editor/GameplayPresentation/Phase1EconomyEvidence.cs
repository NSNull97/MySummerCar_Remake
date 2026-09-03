using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MSC.LegacyImport.Editor.Configuration;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1EconomyEvidence
    {
        public const string LockedSceneSha256 =
            "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4";
        public const string LockedGlobalsSha256 =
            "0c232081e5dd2d6611d27ccd06274cbb31f5fabc5ef5e5f901b86d39da25afec";

        private const string ConfigurationPath =
            "Config/DonorPaths.local.json";
        private const string SceneRelativePath =
            "raw/world/milestone-04a1/assetripper-unity-project/" +
            "ExportedProject/Assets/_Scenes/GAME.unity";
        private const string GlobalsRelativePath =
            "raw/world/milestone-04a1/assetripper-unity-project/" +
            "ExportedProject/Assets/Resources/PlayMakerGlobals.asset";

        public static LockedEconomyEvidence LoadLockedEvidence()
        {
            DonorPathConfiguration paths =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string scenePath = Path.Combine(
                paths.DonorStagingDirectory,
                SceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string globalsPath = Path.Combine(
                paths.DonorStagingDirectory,
                GlobalsRelativePath.Replace('/', Path.DirectorySeparatorChar));
            RequireHash(scenePath, LockedSceneSha256);
            RequireHash(globalsPath, LockedGlobalsSha256);

            decimal playerMoney = ReadNamedDecimal(
                globalsPath,
                "PlayerMoney");
            StorePriceEvidence store = ReadStorePrices(scenePath);
            if (playerMoney != 3000m ||
                store.Keys.Count != 38 ||
                store.Values.Count != 38 ||
                store.InflationRate != 0.054m ||
                store.InitialMultiplier != 1m ||
                store.RestockDay != 4)
            {
                throw new InvalidDataException(
                    "Locked economy evidence no longer matches the approved donor values.");
            }

            return new LockedEconomyEvidence(
                playerMoney,
                store.Keys.ToArray(),
                store.Values.ToArray(),
                store.InflationRate,
                store.InitialMultiplier,
                store.RestockDay);
        }

        private static StorePriceEvidence ReadStorePrices(string scenePath)
        {
            var keys = new List<string>(38);
            var values = new List<decimal>(38);
            var inflationRates = new HashSet<decimal>();
            var initialMultipliers = new HashSet<decimal>();
            var restockDays = new HashSet<int>();
            bool foundPriceTable = false;
            bool inPriceRecord = false;
            ReadMode mode = ReadMode.None;
            string pendingNamedValue = string.Empty;

            using var reader = new StreamReader(scenePath);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed == "name: Inflationrate" ||
                    trimmed == "name: PriceMultiplier" ||
                    trimmed == "name: RestockDay")
                {
                    pendingNamedValue = trimmed.Substring("name: ".Length);
                    continue;
                }

                if (!string.IsNullOrEmpty(pendingNamedValue) &&
                    trimmed.StartsWith("value: ", StringComparison.Ordinal))
                {
                    string raw = trimmed.Substring("value: ".Length);
                    if (pendingNamedValue == "RestockDay")
                    {
                        restockDays.Add(int.Parse(
                            raw,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        decimal parsed = decimal.Parse(
                            raw,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture);
                        if (pendingNamedValue == "Inflationrate")
                        {
                            inflationRates.Add(parsed);
                        }
                        else
                        {
                            initialMultipliers.Add(parsed);
                        }
                    }

                    pendingNamedValue = string.Empty;
                }

                if (!foundPriceTable && trimmed == "referenceName: Prices")
                {
                    foundPriceTable = true;
                    inPriceRecord = true;
                    continue;
                }

                if (!inPriceRecord)
                {
                    continue;
                }

                if (trimmed == "preFillKeyList:")
                {
                    mode = ReadMode.Keys;
                    continue;
                }

                if (trimmed == "preFillFloatList:")
                {
                    mode = ReadMode.Values;
                    continue;
                }

                if (trimmed.StartsWith("preFill", StringComparison.Ordinal) ||
                    trimmed.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    mode = ReadMode.None;
                    if (trimmed.StartsWith("--- !u!", StringComparison.Ordinal))
                    {
                        break;
                    }

                    continue;
                }

                if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    continue;
                }

                string value = trimmed.Substring(2).Trim();
                if (mode == ReadMode.Keys)
                {
                    keys.Add(value);
                }
                else if (mode == ReadMode.Values)
                {
                    values.Add(decimal.Parse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture));
                }
            }

            if (!foundPriceTable || keys.Count != values.Count ||
                inflationRates.Count != 1 || initialMultipliers.Count != 1 ||
                restockDays.Count != 1)
            {
                throw new InvalidDataException(
                    "Could not uniquely decode the locked store economy evidence.");
            }

            return new StorePriceEvidence(
                keys,
                values,
                inflationRates.Single(),
                initialMultipliers.Single(),
                restockDays.Single());
        }

        private static decimal ReadNamedDecimal(string path, string variableName)
        {
            bool pending = false;
            using var reader = new StreamReader(path);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed == "name: " + variableName)
                {
                    pending = true;
                    continue;
                }

                if (pending && trimmed.StartsWith("value: ", StringComparison.Ordinal))
                {
                    return decimal.Parse(
                        trimmed.Substring("value: ".Length),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture);
                }
            }

            throw new InvalidDataException(
                $"Locked variable '{variableName}' was not found in '{path}'.");
        }

        private static void RequireHash(string path, string expected)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Locked economy evidence file is missing.",
                    path);
            }

            using SHA256 algorithm = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            string actual = BitConverter.ToString(
                    algorithm.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Locked evidence hash mismatch for '{path}'. Expected " +
                    $"{expected}, got {actual}.");
            }
        }

        private enum ReadMode
        {
            None,
            Keys,
            Values,
        }

        private readonly struct StorePriceEvidence
        {
            public StorePriceEvidence(
                IReadOnlyList<string> keys,
                IReadOnlyList<decimal> values,
                decimal inflationRate,
                decimal initialMultiplier,
                int restockDay)
            {
                Keys = keys;
                Values = values;
                InflationRate = inflationRate;
                InitialMultiplier = initialMultiplier;
                RestockDay = restockDay;
            }

            public IReadOnlyList<string> Keys { get; }
            public IReadOnlyList<decimal> Values { get; }
            public decimal InflationRate { get; }
            public decimal InitialMultiplier { get; }
            public int RestockDay { get; }
        }
    }

    public sealed class LockedEconomyEvidence
    {
        public LockedEconomyEvidence(
            decimal initialBalanceMarkka,
            string[] storePriceKeys,
            decimal[] storeBasePricesMarkka,
            decimal storeInflationRate,
            decimal storeInitialMultiplier,
            int storeRestockDay)
        {
            InitialBalanceMarkka = initialBalanceMarkka;
            StorePriceKeys = storePriceKeys ?? Array.Empty<string>();
            StoreBasePricesMarkka = storeBasePricesMarkka ??
                Array.Empty<decimal>();
            StoreInflationRate = storeInflationRate;
            StoreInitialMultiplier = storeInitialMultiplier;
            StoreRestockDay = storeRestockDay;
        }

        public decimal InitialBalanceMarkka { get; }
        public string[] StorePriceKeys { get; }
        public decimal[] StoreBasePricesMarkka { get; }
        public decimal StoreInflationRate { get; }
        public decimal StoreInitialMultiplier { get; }
        public int StoreRestockDay { get; }
    }
}
