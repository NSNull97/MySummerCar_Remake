using MSC.Weather.Wetness;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WeatherDomain
{
    public sealed class WetnessDomainTests
    {
        [Test]
        public void Rain_AccumulatesAllExteriorWetnessChannels()
        {
            var controller = new GlobalWetnessController(WetnessConfig.CreateRemakeDesignTarget());

            controller.Advance(new WetnessEnvironmentInputs(
                120d, 0.8f, 2f, 12f, 0f, 0.2f, SurfaceExposureProfile.Exterior));

            Assert.That(controller.State.GroundWetness01, Is.GreaterThan(0f));
            Assert.That(controller.State.RoadWetness01, Is.GreaterThan(0f));
            Assert.That(controller.State.PuddleAmount01, Is.GreaterThan(0f));
            Assert.That(controller.State.VegetationWetness01, Is.GreaterThan(0f));
        }

        [Test]
        public void Drying_IsMonotonicUnderControlledDryInputs()
        {
            var controller = new GlobalWetnessController(
                WetnessConfig.CreateRemakeDesignTarget(),
                new WetnessState(1f, 0.9f, 0.8f, 0.7f));

            WetnessState previous = controller.State;
            for (int index = 0; index < 8; index++)
            {
                controller.Advance(new WetnessEnvironmentInputs(
                    60d, 0f, 8f, 22f, 1f, 1f, SurfaceExposureProfile.Exterior));
                WetnessState current = controller.State;
                Assert.That(current.GroundWetness01, Is.LessThanOrEqualTo(previous.GroundWetness01));
                Assert.That(current.RoadWetness01, Is.LessThanOrEqualTo(previous.RoadWetness01));
                Assert.That(current.PuddleAmount01, Is.LessThanOrEqualTo(previous.PuddleAmount01));
                Assert.That(current.VegetationWetness01, Is.LessThanOrEqualTo(previous.VegetationWetness01));
                previous = current;
            }
        }

        [Test]
        public void InteriorExposure_ExcludesPrecipitationAccumulation()
        {
            var exterior = new GlobalWetnessController(WetnessConfig.CreateRemakeDesignTarget());
            var interior = new GlobalWetnessController(WetnessConfig.CreateRemakeDesignTarget());

            exterior.Advance(new WetnessEnvironmentInputs(
                180d, 1f, 0f, 10f, 0f, 0f, SurfaceExposureProfile.Exterior));
            interior.Advance(new WetnessEnvironmentInputs(
                180d, 1f, 0f, 10f, 0f, 0f, SurfaceExposureProfile.Interior));

            Assert.That(exterior.State.GroundWetness01, Is.GreaterThan(0f));
            Assert.That(interior.State, Is.EqualTo(default(WetnessState)));
        }

        [Test]
        public void ShelterVolume_MapsWorldPositionWithoutHierarchyLookup()
        {
            var volume = new ShelterVolume(
                "shelter.test",
                Vector3.zero,
                new Vector3(2f, 3f, 4f),
                SurfaceExposureProfile.Sheltered);

            Assert.That(volume.Contains(new Vector3(1.9f, 0f, 0f)), Is.True);
            Assert.That(volume.Contains(new Vector3(2.1f, 0f, 0f)), Is.False);
            Assert.That(volume.ExposureProfile.PrecipitationExposure01,
                Is.LessThan(SurfaceExposureProfile.Exterior.PrecipitationExposure01));
        }
    }
}
