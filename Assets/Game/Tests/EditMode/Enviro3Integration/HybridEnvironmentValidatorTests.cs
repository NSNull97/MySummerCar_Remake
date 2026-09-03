using System.Linq;
using MSC.Weather.Enviro3Integration.Editor;
using MSC.Weather.Production;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.Enviro3Integration
{
    public sealed class HybridEnvironmentValidatorTests
    {
        private const string ZoneCatalogPath =
            "Assets/Game/Weather/Production/Content/Zones/" +
            "WeatherZoneCellCatalog.asset";
        private const string NoRainAuditPath =
            "Assets/Game/Weather/Enviro3Integration/Editor/Evidence/" +
            "DonorNoRainCoverageAudit.json";

        [Test]
        public void DonorNoRainAudit_ClassifiesAllStaticAndDynamicRecords()
        {
            WeatherZoneCellCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WeatherZoneCellCatalog>(
                    ZoneCatalogPath);
            TextAsset audit = AssetDatabase.LoadAssetAtPath<TextAsset>(
                NoRainAuditPath);

            Assert.That(catalog, Is.Not.Null, ZoneCatalogPath);
            Assert.That(audit, Is.Not.Null, NoRainAuditPath);
            Assert.That(catalog.ValidateConfiguration(), Is.Empty);
            Assert.That(catalog.Definitions.Count, Is.EqualTo(20));
            Assert.That(
                CountOccurrences(audit.text, "\"sourceStableId\""),
                Is.EqualTo(24));
            Assert.That(
                audit.text,
                Does.Contain("\"staticOrOverlayRecordsCovered\": 23"));
            Assert.That(
                audit.text,
                Does.Contain("\"dynamicVehicleRecordsDeferred\": 1"));
            Assert.That(
                audit.text,
                Does.Contain("DeferredDynamicNotStaticWorldZone"));

            for (int index = 0; index < catalog.Definitions.Count; index++)
            {
                Assert.That(
                    audit.text,
                    Does.Contain(catalog.Definitions[index].StableId),
                    catalog.Definitions[index].StableId);
            }
        }

        [Test]
        public void ZoneValidator_ReportsDuplicateStableIds()
        {
            GameObject firstOwner = new GameObject("DuplicateZone_First_Test");
            GameObject secondOwner = new GameObject("DuplicateZone_Second_Test");
            WeatherZoneProfile profile = ScriptableObject.CreateInstance<WeatherZoneProfile>();
            try
            {
                BoxCollider firstCollider = firstOwner.AddComponent<BoxCollider>();
                BoxCollider secondCollider = secondOwner.AddComponent<BoxCollider>();
                WeatherZone first = firstOwner.AddComponent<WeatherZone>();
                WeatherZone second = secondOwner.AddComponent<WeatherZone>();
                first.ConfigureForAuthoring("zone.duplicate", profile, 0, firstCollider);
                second.ConfigureForAuthoring("zone.duplicate", profile, 1, secondCollider);

                EnvironmentValidationIssue duplicate =
                    WeatherZoneValidator.ValidateLoaded()
                        .First(issue => issue.Code == "ZONE-DUPLICATE");

                Assert.That(duplicate.Severity, Is.EqualTo(EnvironmentValidationSeverity.Error));
                Assert.That(duplicate.Message, Does.Contain("zone.duplicate"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(secondOwner);
                Object.DestroyImmediate(firstOwner);
            }
        }

        private static int CountOccurrences(string value, string token)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(
                       token,
                       offset,
                       System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }

            return count;
        }
    }
}
