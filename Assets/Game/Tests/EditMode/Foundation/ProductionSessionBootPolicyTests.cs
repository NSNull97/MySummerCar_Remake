using MSC.Bootstrap;
using NUnit.Framework;

namespace MSC.Tests.EditMode.Foundation
{
    public sealed class ProductionSessionBootPolicyTests
    {
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        public void PendingNativeRestore_BypassesMainMenu(
            bool configuredStartInMainMenu,
            bool hasPendingNativeRestore,
            bool expected)
        {
            Assert.That(
                ProductionUiInstaller.ShouldBeginInMainMenu(
                    configuredStartInMainMenu,
                    hasPendingNativeRestore),
                Is.EqualTo(expected));
        }
    }
}
