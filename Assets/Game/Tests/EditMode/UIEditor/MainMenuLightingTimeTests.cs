using System;
using MSC.UI.Presentation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.UIEditor
{
    public sealed class MainMenuLightingTimeTests
    {
        [Test]
        public void NoonAndMidnight_PreserveAcceptedDayAndReadableNight()
        {
            MainMenuLightingState noon = MainMenuLightingTime.Evaluate(TimeSpan.FromHours(12));
            MainMenuLightingState midnight = MainMenuLightingTime.Evaluate(TimeSpan.Zero);
            Assert.That(noon.Daylight, Is.EqualTo(1f));
            Assert.That(noon.KeyLux, Is.EqualTo(60000f));
            Assert.That(noon.FillLux, Is.EqualTo(22000f));
            Assert.That(noon.SkyIntensity, Is.EqualTo(6500f));
            Assert.That(noon.LampFactor, Is.EqualTo(0.35f));
            Assert.That(midnight.Night, Is.EqualTo(1f));
            Assert.That(midnight.KeyLux, Is.LessThan(noon.KeyLux * 0.05f));
            Assert.That(midnight.FillLux, Is.InRange(noon.FillLux * 0.25f, noon.FillLux * 0.5f));
            Assert.That(midnight.SkyIntensity, Is.InRange(noon.SkyIntensity * 0.15f, noon.SkyIntensity * 0.35f));
            Assert.That(midnight.LampFactor, Is.EqualTo(1f));
        }

        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(18)]
        [TestCase(20)]
        [TestCase(22)]
        public void PhaseBoundaries_AreContinuousWithoutLightingJumps(int hour)
        {
            TimeSpan boundary = TimeSpan.FromHours(hour);
            MainMenuLightingState before = MainMenuLightingTime.Evaluate(boundary - TimeSpan.FromMilliseconds(100));
            MainMenuLightingState at = MainMenuLightingTime.Evaluate(boundary);
            MainMenuLightingState after = MainMenuLightingTime.Evaluate(boundary + TimeSpan.FromMilliseconds(100));
            AssertContinuous(before, at);
            AssertContinuous(at, after);
        }

        [Test]
        public void FullDay_UsesNormalizedWeightsAndGradualDawnDusk()
        {
            MainMenuLightingState previous = MainMenuLightingTime.Evaluate(TimeSpan.Zero);
            for (int minute = 0; minute <= 24 * 60; minute++)
            {
                MainMenuLightingState state = MainMenuLightingTime.Evaluate(TimeSpan.FromMinutes(minute));
                Assert.That(state.Daylight + state.Twilight + state.Night, Is.EqualTo(1f).Within(0.000001f));
                Assert.That(state.Daylight, Is.InRange(0f, 1f));
                Assert.That(state.Twilight, Is.InRange(0f, 1f));
                Assert.That(state.Night, Is.InRange(0f, 1f));
                Assert.That(state.LampFactor, Is.InRange(0f, 1f));
                if (minute >= 6 * 60 && minute <= 8 * 60)
                    Assert.That(state.KeyLux, Is.GreaterThanOrEqualTo(previous.KeyLux));
                if (minute >= 18 * 60 && minute <= 22 * 60)
                    Assert.That(state.KeyLux, Is.LessThanOrEqualTo(previous.KeyLux));
                previous = state;
            }

            Assert.That(MainMenuLightingTime.Evaluate(TimeSpan.FromHours(7)).Twilight, Is.EqualTo(1f));
            Assert.That(MainMenuLightingTime.Evaluate(TimeSpan.FromHours(20)).Twilight, Is.EqualTo(1f));
        }

        [Test]
        public void MidnightWrapAndArbitraryDates_UseOnlyTimeOfDay()
        {
            AssertContinuous(
                MainMenuLightingTime.Evaluate(TimeSpan.FromDays(1) - TimeSpan.FromMilliseconds(100)),
                MainMenuLightingTime.Evaluate(TimeSpan.Zero));
            MainMenuLightingState morning = MainMenuLightingTime.Evaluate(TimeSpan.FromHours(7.5));
            Assert.That(MainMenuLightingTime.Evaluate(TimeSpan.FromHours(31.5)), Is.EqualTo(morning));
            Assert.That(MainMenuLightingTime.Evaluate(TimeSpan.FromHours(-16.5)), Is.EqualTo(morning));
            Assert.That(MainMenuLightingTime.Evaluate(new DateTime(2026, 1, 1, 7, 30, 0)), Is.EqualTo(morning));
            Assert.That(MainMenuLightingTime.Evaluate(new DateTime(2040, 8, 19, 7, 30, 0)), Is.EqualTo(morning));
        }

        private static void AssertContinuous(MainMenuLightingState left, MainMenuLightingState right)
        {
            Assert.That(right.Daylight, Is.EqualTo(left.Daylight).Within(0.000001f));
            Assert.That(right.Twilight, Is.EqualTo(left.Twilight).Within(0.000001f));
            Assert.That(right.Night, Is.EqualTo(left.Night).Within(0.000001f));
            Assert.That(right.KeyLux, Is.EqualTo(left.KeyLux).Within(0.01f));
            Assert.That(right.FillLux, Is.EqualTo(left.FillLux).Within(0.01f));
            Assert.That(right.SkyIntensity, Is.EqualTo(left.SkyIntensity).Within(0.01f));
            Assert.That(right.LampFactor, Is.EqualTo(left.LampFactor).Within(0.000001f));
            Assert.That(right.SunElevationDegrees, Is.EqualTo(left.SunElevationDegrees).Within(0.01f));
            double angleDelta = Math.Abs(right.SunAzimuthDegrees - left.SunAzimuthDegrees);
            Assert.That(Math.Min(angleDelta, 360d - angleDelta), Is.LessThan(0.01d));
        }
    }
}
