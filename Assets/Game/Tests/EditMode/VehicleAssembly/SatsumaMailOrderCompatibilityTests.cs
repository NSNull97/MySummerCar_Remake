using System.Collections.Generic;
using System.Linq;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaMailOrderCompatibilityTests
    {
        [Test]
        public void DefaultCatalogCoversEveryMailOrderDeliveryExactlyOnce()
        {
            IReadOnlyList<VehicleDeliveredPartCompatibilityRecord> records =
                SatsumaMailOrderCompatibilityDefaults.CreateRecords();

            Assert.That(
                records.Count,
                Is.EqualTo(SatsumaMailOrderCompatibilityDefaults.ExpectedEntryCount));
            Assert.That(
                records.Select(value => value.ItemDefinitionId).Distinct().Count(),
                Is.EqualTo(records.Count));
            Assert.That(
                records.All(value => value.TryValidate(out _)),
                Is.True);
        }

        [Test]
        public void WheelSetDeliveryExpandsToFourStableAssemblyParts()
        {
            VehicleDeliveredPartCompatibilityRecord wheelSet =
                SatsumaMailOrderCompatibilityDefaults.CreateRecords()
                    .Single(value =>
                        value.ItemDefinitionId ==
                        "item.mail-order.wheelset-rally");

            Assert.That(
                wheelSet.ResolutionKind,
                Is.EqualTo(DeliveredPartResolutionKind.MultiPartKit));
            Assert.That(wheelSet.PartDefinitionIds.Count, Is.EqualTo(4));
            Assert.That(
                wheelSet.PartDefinitionIds,
                Has.All.StartsWith("vehicle.satsuma.part.wheel-rally-"));
        }

        [Test]
        public void CatalogBuildsDeterministicLookup()
        {
            VehicleDeliveredPartCompatibilityCatalog catalog =
                ScriptableObject.CreateInstance<
                    VehicleDeliveredPartCompatibilityCatalog>();
            try
            {
                catalog.ConfigureForAuthoring(
                    SatsumaMailOrderCompatibilityDefaults.CatalogId,
                    SatsumaMailOrderCompatibilityDefaults.CreateRecords());

                Assert.That(catalog.ValidateConfiguration(), Is.Empty);
                Assert.That(
                    catalog.TryResolve(
                        "item.mail-order.racing-radiator",
                        out VehicleDeliveredPartCompatibilityRecord radiator),
                    Is.True);
                Assert.That(
                    radiator.PartDefinitionIds.Single(),
                    Is.EqualTo("vehicle.satsuma.part.radiator-racing"));
                Assert.That(
                    catalog.TryResolve("item.mail-order.missing", out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
