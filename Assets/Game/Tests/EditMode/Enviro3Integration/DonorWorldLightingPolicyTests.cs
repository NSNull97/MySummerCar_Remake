using System;
using MSC.Weather.Enviro3Integration;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.Enviro3Integration
{
    public sealed class DonorWorldLightingPolicyTests
    {
        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(3f, 0.21323529f, 0.13797577f, 0.1395329f)]
        [TestCase(5f, 0.6507353f, 0.43721515f, 0.31324613f)]
        [TestCase(7f, 0.93014705f, 0.7778964f, 0.61564666f)]
        [TestCase(12f, 0.9852941f, 0.95731413f, 0.8838668f)]
        [TestCase(19f, 0.93014705f, 0.7778964f, 0.61564666f)]
        [TestCase(21f, 0.64705884f, 0.45912892f, 0.29544225f)]
        [TestCase(23f, 0.20955881f, 0.15988955f, 0.12172902f)]
        public void SunColor_FollowsDonorTwoHourColorStates(
            float hour,
            float expectedGammaR,
            float expectedGammaG,
            float expectedGammaB)
        {
            Color actual = DonorWorldLightingPolicy.EvaluateSunColor(hour / 24f);

            AssertGammaColor(
                actual,
                new Color(expectedGammaR, expectedGammaG, expectedGammaB, 1f));
        }

        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(5f, 0.14111158f, 0.14111158f, 0.21323529f)]
        [TestCase(12f, 0.28222317f, 0.28222317f, 0.42647058f)]
        [TestCase(23f, 0.14111158f, 0.14111158f, 0.21323529f)]
        public void AmbientColor_FollowsDonorFlatFillStates(
            float hour,
            float expectedGammaR,
            float expectedGammaG,
            float expectedGammaB)
        {
            Color actual = DonorWorldLightingPolicy.EvaluateAmbientColor(
                hour / 24f);

            AssertGammaColor(
                actual,
                new Color(expectedGammaR, expectedGammaG, expectedGammaB, 1f));
        }

        [Test]
        public void WorldLighting_WrapsClockAndRetainsDonorStrengths()
        {
            AssertGammaColor(
                DonorWorldLightingPolicy.EvaluateSunColor(1.5f),
                new Color(0.9852941f, 0.95731413f, 0.8838668f, 1f));
            Assert.That(
                DonorWorldLightingPolicy.DonorLegacySunIntensity,
                Is.EqualTo(1.75f));
            Assert.That(
                Enviro3ProductionVisualPolicy.ClearSkyDirectSunlightMultiplier,
                Is.EqualTo(1f));
            Assert.That(
                Enviro3ProductionVisualPolicy.ClearSkySunShadowStrength,
                Is.EqualTo(0.8f));
            Assert.That(
                DonorWorldLightingPolicy.DonorAmbientIntensity,
                Is.EqualTo(1f));
        }

        [Test]
        public void WorldLighting_RejectsNonFiniteClockValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DonorWorldLightingPolicy.EvaluateSunColor(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DonorWorldLightingPolicy.EvaluateAmbientColor(
                    float.PositiveInfinity));
        }

        private static void AssertGammaColor(Color actualLinear, Color expectedGamma)
        {
            Color actualGamma = actualLinear.gamma;
            Assert.That(actualGamma.r, Is.EqualTo(expectedGamma.r).Within(0.0002f));
            Assert.That(actualGamma.g, Is.EqualTo(expectedGamma.g).Within(0.0002f));
            Assert.That(actualGamma.b, Is.EqualTo(expectedGamma.b).Within(0.0002f));
            Assert.That(actualGamma.a, Is.EqualTo(expectedGamma.a).Within(0.0002f));
        }
    }
}
