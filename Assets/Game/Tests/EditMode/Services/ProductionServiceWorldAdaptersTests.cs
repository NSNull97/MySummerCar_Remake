using MSC.Bootstrap;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Services.Tests.EditMode
{
    public sealed class ProductionServiceWorldAdaptersTests
    {
        private GameObject player;
        private ServiceCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("ServiceProximityPlayer");
            catalog = ScriptableObject.CreateInstance<ServiceCatalog>();

            var location = new ServiceLocationDefinition();
            location.ConfigureForAuthoring(
                "service.location.tests.proximity",
                "Test service",
                "service.source.tests.proximity",
                ServiceLocationKind.Store,
                "service.anchor.tests.interaction",
                "service.anchor.tests.handoff",
                new Vector3(10f, 2f, 3f),
                new[]
                {
                    CreateAlwaysAvailableWindow(),
                });
            catalog.ConfigureForAuthoring(
                "catalog.services.tests.proximity",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                new[] { location },
                System.Array.Empty<ServiceOfferDefinition>(),
                System.Array.Empty<ServiceFuelPriceDefinition>());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Proximity_UsesAuthoredWorldPositionAndFailsClosed()
        {
            var proximity = new ProductionPlayerServiceProximitySource(
                catalog,
                player.transform);
            player.transform.position = new Vector3(13f, 6f, 3f);

            Assert.That(
                proximity.IsPlayerWithin(
                    "service.location.tests.proximity",
                    5f),
                Is.True);
            Assert.That(
                proximity.IsPlayerWithin(
                    "service.location.tests.proximity",
                    4.99f),
                Is.False);
            Assert.That(
                proximity.IsPlayerWithin(
                    "service.location.tests.unknown",
                    100f),
                Is.False);
            Assert.That(
                proximity.IsPlayerWithin(
                    "service.location.tests.proximity",
                    float.NaN),
                Is.False);
            Assert.That(
                proximity.IsPlayerWithin(
                    "service.location.tests.proximity",
                    -1f),
                Is.False);
        }

        private static ServiceAvailabilityWindow
            CreateAlwaysAvailableWindow()
        {
            var window = new ServiceAvailabilityWindow();
            window.ConfigureForAuthoring(
                (1 << 7) - 1,
                0,
                0);
            return window;
        }
    }
}
