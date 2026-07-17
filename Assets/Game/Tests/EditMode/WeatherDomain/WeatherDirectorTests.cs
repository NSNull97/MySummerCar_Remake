using System;
using MSC.Weather.Domain;
using MSC.Weather.Wetness;
using NUnit.Framework;

namespace MSC.Tests.EditMode.WeatherDomain
{
    public sealed class WeatherDirectorTests
    {
        [Test]
        public void Override_HigherPriorityWins_AndRemovalRevealsProgressedSchedule()
        {
            WeatherDirector director = CreateDirector();
            director.AddOverride(new WeatherOverride(
                "test.low", "tests", "lower priority", 10, 0d, 100d,
                WeatherStateIds.Overcast, WeatherOverrideSerializationPolicy.Transient));
            director.AddOverride(new WeatherOverride(
                "test.high", "tests", "higher priority", 20, 0d, 10d,
                WeatherStateIds.HeavyRain, WeatherOverrideSerializationPolicy.Transient));

            Assert.That(director.CurrentState.Id, Is.EqualTo(WeatherStateIds.HeavyRain));
            director.Advance(11d);

            Assert.That(director.CurrentState.Id, Is.EqualTo(WeatherStateIds.Overcast));
            Assert.That(director.Timeline.Transition.ElapsedSeconds, Is.EqualTo(11d));
            Assert.That(director.RemoveOverride("test.low"), Is.True);
            Assert.That(director.CurrentState.Id, Is.Not.EqualTo(WeatherStateIds.Overcast));
        }

        [Test]
        public void Override_EqualPriority_UsesMonotonicSequenceTieBreak()
        {
            WeatherDirector director = CreateDirector();
            ulong first = director.AddOverride(new WeatherOverride(
                "test.first", "tests", "first", 5, 0d, 30d,
                WeatherStateIds.Overcast, WeatherOverrideSerializationPolicy.Transient));
            ulong second = director.AddOverride(new WeatherOverride(
                "test.second", "tests", "second", 5, 0d, 30d,
                WeatherStateIds.Drizzle, WeatherOverrideSerializationPolicy.Transient));

            Assert.That(second, Is.GreaterThan(first));
            Assert.That(director.CurrentState.Id, Is.EqualTo(WeatherStateIds.Drizzle));
        }

        [Test]
        public void FrozenSchedule_DoesNotConsumeTimelineButOverrideLifetimeStillAdvances()
        {
            WeatherDirector director = CreateDirector();
            double elapsed = director.Timeline.Transition.ElapsedSeconds;
            director.SetScheduleFrozen(true);
            director.AddOverride(new WeatherOverride(
                "test.short", "tests", "lifetime", 1, 0d, 2d,
                WeatherStateIds.Thunderstorm, WeatherOverrideSerializationPolicy.Transient));

            director.Advance(3d);

            Assert.That(director.Timeline.Transition.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(director.TryGetActiveOverride(out _), Is.False);
        }

        [Test]
        public void EnvironmentOutputs_AreVendorNeutralAndStable()
        {
            WeatherDirector director = CreateDirector();
            var wetness = new WetnessEnvironmentOutputs(
                new WetnessState(0.2f, 0.3f, 0.1f, 0.4f),
                SurfaceExposureProfile.Exterior.StableId,
                4U);
            var context = new WeatherEnvironmentOutputContext(
                new WeatherClockOutput(1995, 8, 1, 0, 0.5f),
                wetness,
                WeatherExposureContext.Exterior,
                new WeatherPresentationStatusOutput("quality.high", WeatherPresentationHealth.Ready, 7U));

            WeatherEnvironmentOutputs outputs = director.CreateEnvironmentOutputs(context);

            Assert.That(outputs.Weather.Id, Is.EqualTo(director.CurrentState.Id));
            Assert.That(outputs.Wetness.RoadWetness01, Is.EqualTo(0.3f));
            Assert.That(outputs.Audio.PrecipitationIntensity01, Is.EqualTo(director.RainIntensity));
            Assert.That(outputs.Ui.NormalizedDayTime01, Is.EqualTo(0.5f));
            Assert.That(outputs.PresentationStatus.PresentedRevision, Is.EqualTo(7U));
        }

        [Test]
        public void Advance_WhenScheduleSafetyBoundIsExceeded_RestoresCompleteSnapshot()
        {
            WeatherDirector director = CreateDirector();
            WeatherSnapshot before = director.CaptureSnapshot();

            Assert.That(
                () => director.Advance(double.MaxValue),
                Throws.TypeOf<InvalidOperationException>());

            WeatherSnapshot after = director.CaptureSnapshot();
            Assert.That(after.SimulationSeconds, Is.EqualTo(before.SimulationSeconds));
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.NextOverrideSequence, Is.EqualTo(before.NextOverrideSequence));
            Assert.That(after.Schedule.CurrentProfileId, Is.EqualTo(before.Schedule.CurrentProfileId));
            Assert.That(after.Schedule.TargetProfileId, Is.EqualTo(before.Schedule.TargetProfileId));
            Assert.That(after.Schedule.FrontDurationSeconds, Is.EqualTo(before.Schedule.FrontDurationSeconds));
            Assert.That(after.Schedule.TransitionDurationSeconds, Is.EqualTo(before.Schedule.TransitionDurationSeconds));
            Assert.That(after.Schedule.ElapsedSeconds, Is.EqualTo(before.Schedule.ElapsedSeconds));
            Assert.That(after.Schedule.Cursor, Is.EqualTo(before.Schedule.Cursor));
            Assert.That(after.Schedule.RandomState, Is.EqualTo(before.Schedule.RandomState));

            Assert.That(() => director.Advance(1d), Throws.Nothing);
            Assert.That(director.SimulationSeconds, Is.EqualTo(1d));
        }

        private static WeatherDirector CreateDirector() => new WeatherDirector(
            WeatherProfileCatalog.CreateRemakeDesignTargets(),
            new WeatherSeed(123UL, 9UL),
            WeatherStateIds.Clear);
    }
}
