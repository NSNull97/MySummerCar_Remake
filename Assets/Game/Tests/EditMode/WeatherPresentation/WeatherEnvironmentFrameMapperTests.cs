using MSC.Weather.Domain;
using MSC.Weather.Presentation;
using MSC.Weather.Wetness;
using NUnit.Framework;

namespace MSC.Tests.EditMode.WeatherPresentation
{
    public sealed class WeatherEnvironmentFrameMapperTests
    {
        [Test]
        public void TryMap_Thunderstorm_ProducesStableStormFrame()
        {
            WeatherEnvironmentOutputs outputs = CreateOutputs(WeatherStateIds.Thunderstorm);

            bool mapped = WeatherEnvironmentFrameMapper.TryMap(
                outputs,
                7UL,
                EnvironmentQualityTier.High,
                120f,
                EnvironmentLightningVisualRequest.None,
                EnvironmentRefreshRequest.None,
                out EnvironmentPresentationFrame frame,
                out string failure);

            Assert.That(mapped, Is.True, failure);
            Assert.That(frame.Revision, Is.EqualTo(7UL));
            Assert.That(frame.BindingId.Value, Is.EqualTo("weather.storm_visual"));
            Assert.That(frame.CloudType, Is.EqualTo(EnvironmentCloudType.Storm));
            Assert.That(frame.PrecipitationType, Is.EqualTo(EnvironmentPrecipitationType.Rain));
            Assert.That(frame.VisibilityMeters, Is.EqualTo(2200f));
            Assert.That(
                frame.ExposureContext,
                Is.EqualTo(WeatherExposureContext.Exterior));
            Assert.That(frame.TransitionDurationSeconds, Is.EqualTo(120f));
        }

        [Test]
        public void TryMap_DefaultOutputs_DoesNotProducePartialFrame()
        {
            WeatherEnvironmentOutputs outputs = default;

            bool mapped = WeatherEnvironmentFrameMapper.TryMap(
                outputs,
                1UL,
                EnvironmentQualityTier.High,
                0f,
                EnvironmentLightningVisualRequest.None,
                EnvironmentRefreshRequest.None,
                out EnvironmentPresentationFrame frame,
                out string failure);

            Assert.That(mapped, Is.False);
            Assert.That(frame.Enabled, Is.False);
            Assert.That(failure, Does.Contain("stable presentation binding ID"));
        }

        private static WeatherEnvironmentOutputs CreateOutputs(WeatherStateId id)
        {
            WeatherState state = WeatherProfileCatalog
                .CreateRemakeDesignTargets()
                .Get(id)
                .TargetState;
            var context = new WeatherEnvironmentOutputContext(
                new WeatherClockOutput(1995, 8, 1, 0, 0.5f),
                new WetnessEnvironmentOutputs(
                    new WetnessState(0f, 0f, 0f, 0f),
                    SurfaceExposureProfile.Exterior.StableId,
                    1U),
                WeatherExposureContext.Exterior,
                new WeatherPresentationStatusOutput(
                    "quality.high",
                    WeatherPresentationHealth.Ready,
                    0));
            return WeatherEnvironmentOutputs.Compose(state, 1, context);
        }
    }
}
