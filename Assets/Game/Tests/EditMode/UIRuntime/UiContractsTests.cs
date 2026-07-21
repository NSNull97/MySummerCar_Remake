using System.Collections.Generic;
using MSC.UI.Runtime.Capabilities;
using MSC.UI.Runtime.Localization;
using MSC.UI.Runtime.Routing;
using NUnit.Framework;

namespace MSC.Tests.EditMode.UIRuntime
{
    public sealed class UiContractsTests
    {
        [Test]
        public void MissingCapability_IsExplicitlyUnavailable()
        {
            UiCapabilitySet capabilities = new UiCapabilitySet();

            UiCapabilityState state = capabilities.Get(UiCapabilityId.SaveStorage);

            Assert.That(state.Availability, Is.EqualTo(UiCapabilityAvailability.Unavailable));
            Assert.That(state.IsInteractive, Is.False);
            Assert.That(state.ReasonLocalizationKey, Is.EqualTo("ui.capability.reason.unspecified"));
        }

        [Test]
        public void BoundedDefaults_DoNotClaimUnsupportedFeatures()
        {
            UiCapabilitySet release = UiCapabilitySet.CreateBounded08ADefaults(false);
            UiCapabilitySet development = UiCapabilitySet.CreateBounded08ADefaults(true);

            Assert.That(release.Get(UiCapabilityId.SaveStorage).IsInteractive, Is.False);
            Assert.That(release.Get(UiCapabilityId.GraphicsRayTracing).IsInteractive, Is.False);
            Assert.That(release.Get(UiCapabilityId.AudioEndpointEnumeration).IsInteractive, Is.False);
            Assert.That(release.Get(UiCapabilityId.PlayerNeedsProvider).IsInteractive, Is.False);
            Assert.That(release.Get(UiCapabilityId.DeveloperTools).IsInteractive, Is.False);
            Assert.That(development.Get(UiCapabilityId.DeveloperTools).Availability,
                Is.EqualTo(UiCapabilityAvailability.DevelopmentOnly));
        }

        [Test]
        public void RouteAndLocalizationKeys_AreStablePrefixedAndUnique()
        {
            HashSet<UiRouteId> routes = new HashSet<UiRouteId>();
            HashSet<string> routeKeys = new HashSet<string>();
            foreach (UiRouteDefinition route in UiRouteCatalog.All)
            {
                Assert.That(routes.Add(route.Id), Is.True, $"Duplicate route {route.Id}.");
                Assert.That(routeKeys.Add(route.LocalizationKey), Is.True, $"Duplicate route key {route.LocalizationKey}.");
                Assert.That(route.LocalizationKey, Does.StartWith("ui.route."));
            }

            HashSet<string> localizationKeys = new HashSet<string>();
            foreach (string key in UiLocalizationKeys.All)
            {
                Assert.That(localizationKeys.Add(key), Is.True, $"Duplicate localization key {key}.");
                Assert.That(key, Does.StartWith("ui."));
            }
        }
    }
}
