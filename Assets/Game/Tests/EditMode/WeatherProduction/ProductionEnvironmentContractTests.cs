using System;
using MSC.Weather.Domain;
using MSC.Weather.Presentation;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WeatherProduction
{
    public sealed class ProductionEnvironmentContractTests
    {
        [TestCase(EnvironmentQualityTier.Low, "quality.low")]
        [TestCase(EnvironmentQualityTier.Medium, "quality.medium")]
        [TestCase(EnvironmentQualityTier.High, "quality.high")]
        public void QualityTiers_HaveStableProjectOwnedIds(
            EnvironmentQualityTier tier,
            string expected)
        {
            Assert.That(
                ProductionEnvironmentQuality.GetStableId(tier),
                Is.EqualTo(expected));
        }

        [Test]
        public void SerializedQualityValues_PreservePre07CCompatibility()
        {
            Assert.That((int)EnvironmentQualityTier.Low, Is.EqualTo(0));
            Assert.That((int)EnvironmentQualityTier.High, Is.EqualTo(1));
            Assert.That((int)EnvironmentQualityTier.Medium, Is.EqualTo(2));
        }

        [Test]
        public void ShelterResolver_UsesStrongestContainingExposure()
        {
            var volumes = new[]
            {
                new ShelterVolume(
                    "weather.shelter.porch",
                    Vector3.zero,
                    new Vector3(4f, 2f, 4f),
                    SurfaceExposureProfile.Sheltered),
                new ShelterVolume(
                    "weather.shelter.room",
                    Vector3.zero,
                    Vector3.one,
                    SurfaceExposureProfile.Interior),
            };

            Assert.That(
                ProductionShelterResolver.Resolve(Vector3.zero, volumes),
                Is.EqualTo(WeatherExposureContext.Interior));
            Assert.That(
                ProductionShelterResolver.Resolve(
                    new Vector3(2f, 0f, 0f),
                    volumes),
                Is.EqualTo(WeatherExposureContext.Sheltered));
            Assert.That(
                ProductionShelterResolver.Resolve(
                    new Vector3(10f, 0f, 0f),
                    volumes),
                Is.EqualTo(WeatherExposureContext.Exterior));
        }

        [Test]
        public void ShelterResolver_RejectsNonFiniteListener()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ProductionShelterResolver.Resolve(
                    new Vector3(float.NaN, 0f, 0f),
                    Array.Empty<ShelterVolume>()));
        }

        [Test]
        public void RoadWetnessHook_PreservesDryPhysicsUntilCalibration()
        {
            var output = new ProductionRoadWetnessOutput(0.85f);

            Assert.That(output.RoadWetness01, Is.EqualTo(0.85f));
            Assert.That(output.WetFrictionEnabled, Is.False);
            Assert.That(output.FrictionMultiplier, Is.EqualTo(1f));
        }
    }
}
