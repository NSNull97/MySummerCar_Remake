using System;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Persistence;
using MSC.Weather.Wetness;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WeatherDomain
{
    public sealed class WeatherPersistenceTests
    {
        [Test]
        public void DomainDto_JsonRoundTrip_RestoresDeterministicState()
        {
            CreateDomain(44UL, out WeatherDirector weather, out GlobalWetnessController wetness, out LightningStrikeDirector lightning);
            weather.Advance(321d);
            weather.AddOverride(new WeatherOverride(
                "capture.saved", "tests", "round trip", 8, 0d, 1000d,
                WeatherStateIds.SteadyRain, WeatherOverrideSerializationPolicy.Save));
            weather.AddOverride(new WeatherOverride(
                "capture.transient", "tests", "must not persist", 9, 0d, 1000d,
                WeatherStateIds.HeavyRain, WeatherOverrideSerializationPolicy.Transient));
            wetness.Advance(new WetnessEnvironmentInputs(
                240d, 0.8f, 3f, 14f, 0f, 0.5f, SurfaceExposureProfile.Exterior));
            lightning.Advance(30d);
            lightning.TryCreateGameplayStrike(
                new[] { new LightningStrikeCandidate("tower", new Vector3(100f, 30f, 0f), 1f, 30f, 1f, false) },
                null,
                Vector3.zero,
                0.9f,
                out _);

            WeatherDomainSaveDto captured = WeatherDomainPersistence.Capture(weather, wetness, lightning);
            string json = JsonUtility.ToJson(captured);
            WeatherDomainSaveDto decoded = JsonUtility.FromJson<WeatherDomainSaveDto>(json);
            CreateDomain(999UL, out WeatherDirector restoredWeather, out GlobalWetnessController restoredWetness, out LightningStrikeDirector restoredLightning);

            WeatherDomainPersistence.RestoreAtomic(decoded, restoredWeather, restoredWetness, restoredLightning);

            Assert.That(restoredWeather.SimulationSeconds, Is.EqualTo(weather.SimulationSeconds));
            Assert.That(restoredWeather.Timeline.Transition.Front.From, Is.EqualTo(weather.Timeline.Transition.Front.From));
            Assert.That(restoredWeather.Timeline.Transition.Front.To, Is.EqualTo(weather.Timeline.Transition.Front.To));
            Assert.That(restoredWeather.TryGetActiveOverride(out WeatherOverride active), Is.True);
            Assert.That(active.OverrideId, Is.EqualTo("capture.saved"));
            Assert.That(restoredWetness.State, Is.EqualTo(wetness.State));
            Assert.That(restoredLightning.Sequence, Is.EqualTo(lightning.Sequence));
            Assert.That(restoredLightning.IsGameplayStrikeAllowed, Is.False, "Restore grace must be re-applied.");

            weather.Advance(500d);
            restoredWeather.Advance(500d);
            WeatherScheduleSnapshot expectedSchedule = weather.CaptureSnapshot().Schedule;
            WeatherScheduleSnapshot actualSchedule = restoredWeather.CaptureSnapshot().Schedule;
            Assert.That(actualSchedule.CurrentProfileId, Is.EqualTo(expectedSchedule.CurrentProfileId));
            Assert.That(actualSchedule.TargetProfileId, Is.EqualTo(expectedSchedule.TargetProfileId));
            Assert.That(actualSchedule.ElapsedSeconds, Is.EqualTo(expectedSchedule.ElapsedSeconds));
            Assert.That(actualSchedule.RandomState, Is.EqualTo(expectedSchedule.RandomState));
        }

        [Test]
        public void InvalidWeatherDto_DoesNotPartiallyMutateAnyDomain()
        {
            CreateDomain(12UL, out WeatherDirector weather, out GlobalWetnessController wetness, out LightningStrikeDirector lightning);
            weather.Advance(25d);
            wetness.SetState(new WetnessState(0.1f, 0.2f, 0.3f, 0.4f));
            lightning.Advance(7d);
            WeatherSnapshot beforeWeather = weather.CaptureSnapshot();
            WetnessSnapshot beforeWetness = wetness.CaptureSnapshot();
            LightningDirectorSnapshot beforeLightning = lightning.CaptureSnapshot();
            WeatherDomainSaveDto dto = WeatherDomainPersistence.Capture(weather, wetness, lightning);
            dto.Weather.CurrentProfileId = "weather.unknown";

            Assert.Throws<ArgumentException>(() =>
                WeatherDomainPersistence.RestoreAtomic(dto, weather, wetness, lightning));

            Assert.That(weather.CaptureSnapshot().SimulationSeconds, Is.EqualTo(beforeWeather.SimulationSeconds));
            Assert.That(weather.CaptureSnapshot().Schedule.CurrentProfileId, Is.EqualTo(beforeWeather.Schedule.CurrentProfileId));
            Assert.That(wetness.CaptureSnapshot().State, Is.EqualTo(beforeWetness.State));
            Assert.That(lightning.CaptureSnapshot().SimulationSeconds, Is.EqualTo(beforeLightning.SimulationSeconds));
        }

        [Test]
        public void InvalidNumericWetnessDto_IsRejected()
        {
            CreateDomain(5UL, out WeatherDirector weather, out GlobalWetnessController wetness, out LightningStrikeDirector lightning);
            WeatherDomainSaveDto dto = WeatherDomainPersistence.Capture(weather, wetness, lightning);
            dto.Wetness.RoadWetness01 = float.NaN;

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WeatherDomainPersistence.Validate(dto, weather, wetness, lightning));
        }

        [Test]
        public void Capture_IgnoresTransientOverrideAndSerializesOnlySavePolicy()
        {
            CreateDomain(31UL, out WeatherDirector weather, out GlobalWetnessController wetness, out LightningStrikeDirector lightning);
            weather.AddOverride(new WeatherOverride(
                "capture.saved", "tests", "must persist", 5, 0d, 100d,
                WeatherStateIds.SteadyRain, WeatherOverrideSerializationPolicy.Save));
            weather.AddOverride(new WeatherOverride(
                "capture.transient", "tests", "must not persist", 10, 0d, 100d,
                WeatherStateIds.Thunderstorm, WeatherOverrideSerializationPolicy.Transient));

            WeatherDomainSaveDto dto = WeatherDomainPersistence.Capture(weather, wetness, lightning);

            Assert.That(dto.Weather.Overrides, Has.Count.EqualTo(1));
            Assert.That(dto.Weather.Overrides[0].OverrideId, Is.EqualTo("capture.saved"));
            Assert.That(
                dto.Weather.Overrides[0].SerializationPolicy,
                Is.EqualTo((int)WeatherOverrideSerializationPolicy.Save));
        }

        [Test]
        public void ExternalDto_WithTransientOverridePolicy_IsRejectedWithoutMutation()
        {
            CreateDomain(32UL, out WeatherDirector weather, out GlobalWetnessController wetness, out LightningStrikeDirector lightning);
            weather.Advance(25d);
            WeatherSnapshot beforeWeather = weather.CaptureSnapshot();
            WetnessSnapshot beforeWetness = wetness.CaptureSnapshot();
            LightningDirectorSnapshot beforeLightning = lightning.CaptureSnapshot();
            WeatherDomainSaveDto dto = WeatherDomainPersistence.Capture(weather, wetness, lightning);
            dto.Weather.Overrides.Add(new WeatherOverrideSaveDto
            {
                OverrideId = "external.transient",
                Owner = "tests",
                Reason = "must be rejected",
                Priority = 100,
                StartSimulationSeconds = 0d,
                EndSimulationSeconds = 1000d,
                RequestedProfileId = WeatherStateIds.Thunderstorm.Value,
                SerializationPolicy = (int)WeatherOverrideSerializationPolicy.Transient,
                SequenceBits = 1L,
            });

            Assert.Throws<ArgumentException>(() =>
                WeatherDomainPersistence.RestoreAtomic(dto, weather, wetness, lightning));

            Assert.That(weather.CaptureSnapshot().SimulationSeconds, Is.EqualTo(beforeWeather.SimulationSeconds));
            Assert.That(weather.CaptureSnapshot().Overrides, Is.EqualTo(beforeWeather.Overrides));
            Assert.That(wetness.CaptureSnapshot().State, Is.EqualTo(beforeWetness.State));
            Assert.That(lightning.CaptureSnapshot().SimulationSeconds, Is.EqualTo(beforeLightning.SimulationSeconds));
        }

        private static void CreateDomain(
            ulong seed,
            out WeatherDirector weather,
            out GlobalWetnessController wetness,
            out LightningStrikeDirector lightning)
        {
            weather = new WeatherDirector(
                WeatherProfileCatalog.CreateRemakeDesignTargets(),
                new WeatherSeed(seed, 3UL),
                WeatherStateIds.Clear);
            wetness = new GlobalWetnessController(WetnessConfig.CreateRemakeDesignTarget());
            lightning = new LightningStrikeDirector(
                LightningStrikeConfig.CreateRemakeDesignTarget(),
                new WeatherSeed(seed, 17UL));
        }
    }
}
