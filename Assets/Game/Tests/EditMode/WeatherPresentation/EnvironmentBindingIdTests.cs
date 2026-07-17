using MSC.Weather.Presentation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.WeatherPresentation
{
    public sealed class EnvironmentBindingIdTests
    {
        [TestCase("weather.clear")]
        [TestCase("weather.overcast")]
        [TestCase("weather.rain")]
        [TestCase("weather.storm")]
        [TestCase("weather.night")]
        [TestCase("weather.mist")]
        [TestCase("lab.clear-01")]
        public void TryParse_CanonicalStableIds_Accepts(string serializedValue)
        {
            bool parsed = EnvironmentBindingId.TryParse(serializedValue, out EnvironmentBindingId bindingId);

            Assert.That(parsed, Is.True);
            Assert.That(bindingId.IsValid, Is.True);
            Assert.That(bindingId.Value, Is.EqualTo(serializedValue));
            Assert.That(bindingId.ToString(), Is.EqualTo(serializedValue));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("ab")]
        [TestCase("Weather.clear")]
        [TestCase("weather clear")]
        [TestCase("weather/clear")]
        [TestCase(".weather")]
        [TestCase("weather.")]
        [TestCase("weather..clear")]
        [TestCase("weather.-clear")]
        public void TryParse_NonCanonicalOrDisplayIds_Rejects(string serializedValue)
        {
            bool parsed = EnvironmentBindingId.TryParse(serializedValue, out EnvironmentBindingId bindingId);

            Assert.That(parsed, Is.False);
            Assert.That(bindingId, Is.EqualTo(default(EnvironmentBindingId)));
            Assert.That(bindingId.IsValid, Is.False);
        }

        [Test]
        public void TryParse_OverMaximumLength_Rejects()
        {
            string serializedValue = "a" + new string('b', EnvironmentBindingId.MaximumLength);

            Assert.That(EnvironmentBindingId.TryParse(serializedValue, out _), Is.False);
        }

        [Test]
        public void Equality_UsesOrdinalCanonicalValue()
        {
            Assert.That(EnvironmentBindingId.TryParse("weather.clear", out EnvironmentBindingId first), Is.True);
            Assert.That(EnvironmentBindingId.TryParse("weather.clear", out EnvironmentBindingId second), Is.True);
            Assert.That(EnvironmentBindingId.TryParse("weather.rain", out EnvironmentBindingId third), Is.True);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != third, Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }
    }
}
