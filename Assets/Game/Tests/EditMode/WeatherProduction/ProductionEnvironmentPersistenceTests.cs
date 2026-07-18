using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Persistence;
using MSC.Weather.Presentation;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using NUnit.Framework;

namespace MSC.Tests.EditMode.WeatherProduction
{
    public sealed class ProductionEnvironmentPersistenceTests
    {
        [Test]
        public void CaptureAndRestore_PreservesAllAuthoritativeDomainsAndQuality()
        {
            Fixture fixture = CreateFixture();
            fixture.Time.Advance(12.5d);
            fixture.Weather.Advance(240d);
            fixture.Wetness.Advance(CreateWetnessInputs(15d, 0.8f));
            fixture.Lightning.Advance(35d);
            ProductionEnvironmentSaveDto captured =
                ProductionEnvironmentPersistence.Capture(
                    fixture.Time,
                    fixture.Weather,
                    fixture.Wetness,
                    fixture.Lightning,
                    EnvironmentQualityTier.Medium);

            fixture.Time.Advance(30d);
            fixture.Weather.Advance(500d);
            fixture.Wetness.SetState(new WetnessState(0f, 0f, 0f, 0f));
            fixture.Lightning.Advance(100d);

            EnvironmentQualityTier restoredQuality =
                ProductionEnvironmentPersistence.RestoreAtomic(
                    captured,
                    fixture.Time,
                    fixture.Weather,
                    fixture.Wetness,
                    fixture.Lightning);

            Assert.That(restoredQuality, Is.EqualTo(EnvironmentQualityTier.Medium));
            Assert.That(
                fixture.Time.CaptureDto().elapsedGameTicks,
                Is.EqualTo(captured.GameTime.elapsedGameTicks));
            Assert.That(
                fixture.Weather.SimulationSeconds,
                Is.EqualTo(captured.WeatherDomain.Weather.SimulationSeconds));
            Assert.That(
                fixture.Wetness.State.GroundWetness01,
                Is.EqualTo(captured.WeatherDomain.Wetness.GroundWetness01));
            Assert.That(
                fixture.Lightning.SimulationSeconds,
                Is.EqualTo(captured.WeatherDomain.Lightning.SimulationSeconds));
            Assert.That(fixture.Lightning.IsGameplayStrikeAllowed, Is.False,
                "Restore must reapply the project-owned spawn/load lightning grace.");
        }

        [Test]
        public void InvalidWeatherPayload_IsRejectedBeforeTimeMutation()
        {
            Fixture fixture = CreateFixture();
            fixture.Time.Advance(3d);
            ProductionEnvironmentSaveDto dto =
                ProductionEnvironmentPersistence.Capture(
                    fixture.Time,
                    fixture.Weather,
                    fixture.Wetness,
                    fixture.Lightning,
                    EnvironmentQualityTier.Low);
            long beforeTicks = fixture.Time.CaptureState().ElapsedGameTicks;
            dto.WeatherDomain.Weather.ConfigId = "weather.invalid";

            Assert.Throws<System.ArgumentException>(() =>
                ProductionEnvironmentPersistence.RestoreAtomic(
                    dto,
                    fixture.Time,
                    fixture.Weather,
                    fixture.Wetness,
                    fixture.Lightning));
            Assert.That(
                fixture.Time.CaptureState().ElapsedGameTicks,
                Is.EqualTo(beforeTicks));
        }

        [Test]
        public void UnsupportedEnvelopeOrQuality_IsRejected()
        {
            Fixture fixture = CreateFixture();
            ProductionEnvironmentSaveDto dto =
                ProductionEnvironmentPersistence.Capture(
                    fixture.Time,
                    fixture.Weather,
                    fixture.Wetness,
                    fixture.Lightning,
                    EnvironmentQualityTier.High);
            dto.QualityTier = 999;

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                ProductionEnvironmentPersistence.Validate(
                    dto,
                    fixture.Time,
                    fixture.Weather,
                    fixture.Wetness,
                    fixture.Lightning));
        }

        private static Fixture CreateFixture()
        {
            var catalog = WeatherProfileCatalog.CreateRemakeDesignTargets();
            return new Fixture(
                new GameTimeService(),
                new WeatherDirector(
                    catalog,
                    new WeatherSeed(42UL, 7UL),
                    WeatherStateIds.Clear),
                new GlobalWetnessController(
                    WetnessConfig.CreateRemakeDesignTarget()),
                new LightningStrikeDirector(
                    LightningStrikeConfig.CreateRemakeDesignTarget(),
                    new WeatherSeed(43UL, 11UL)));
        }

        private static WetnessEnvironmentInputs CreateWetnessInputs(
            double seconds,
            float precipitation) =>
            new WetnessEnvironmentInputs(
                seconds,
                precipitation,
                6f,
                12f,
                0.5f,
                0.5f,
                SurfaceExposureProfile.Exterior);

        private readonly struct Fixture
        {
            public Fixture(
                GameTimeService time,
                WeatherDirector weather,
                GlobalWetnessController wetness,
                LightningStrikeDirector lightning)
            {
                Time = time;
                Weather = weather;
                Wetness = wetness;
                Lightning = lightning;
            }

            public GameTimeService Time { get; }
            public WeatherDirector Weather { get; }
            public GlobalWetnessController Wetness { get; }
            public LightningStrikeDirector Lightning { get; }
        }
    }
}
