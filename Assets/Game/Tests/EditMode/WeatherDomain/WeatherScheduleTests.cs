using System;
using System.Reflection;
using MSC.Weather.Domain;
using NUnit.Framework;

namespace MSC.Tests.EditMode.WeatherDomain
{
    public sealed class WeatherScheduleTests
    {
        [Test]
        public void DefaultCatalog_ContainsNineStableWeatherStates()
        {
            WeatherProfileCatalog catalog = WeatherProfileCatalog.CreateRemakeDesignTargets();

            Assert.That(catalog.Profiles.Count, Is.EqualTo(9));
            for (int index = 0; index < WeatherStateIds.All.Count; index++)
            {
                Assert.That(catalog.Contains(WeatherStateIds.All[index]), Is.True);
                Assert.That(catalog.Get(WeatherStateIds.All[index]).Provenance,
                    Is.EqualTo(WeatherProfileCatalog.RemakeDesignTargetProvenance));
            }

            WeatherProfile denseFog =
                catalog.Get(WeatherStateIds.DenseFog);
            Assert.That(
                denseFog.TargetState.PresentationBindingId,
                Is.EqualTo("weather.dense_fog"));
            Assert.That(
                denseFog.TargetState.FogIntensity01,
                Is.EqualTo(1f));
            Assert.That(
                denseFog.TargetState.VisibilityMeters,
                Is.EqualTo(80f));
        }

        [Test]
        public void WeatherRuntimeAssembly_DoesNotReferenceEnviro()
        {
            AssemblyName[] references = typeof(WeatherDirector).Assembly.GetReferencedAssemblies();

            for (int index = 0; index < references.Length; index++)
            {
                Assert.That(references[index].Name, Does.Not.Contain("Enviro"));
            }
        }

        [Test]
        public void WeatherRandom_KnownSeed_ProducesVersionedPcgSequence()
        {
            var random = new WeatherRandom(new WeatherSeed(42UL, 54UL));

            Assert.That(random.NextUInt(), Is.EqualTo(0xa15c02b7U));
            Assert.That(random.NextUInt(), Is.EqualTo(0x7b47f409U));
            Assert.That(random.NextUInt(), Is.EqualTo(0xba1d3330U));
            Assert.That(random.NextUInt(), Is.EqualTo(0x83d2f293U));
            Assert.That(random.NextUInt(), Is.EqualTo(0xbfa4784bU));
            Assert.That(random.CaptureState().Version, Is.EqualTo(WeatherRandomState.CurrentVersion));
        }

        [Test]
        public void Schedule_SameSeedAndSteps_ProducesSameTimeline()
        {
            WeatherProfileCatalog catalog = WeatherProfileCatalog.CreateRemakeDesignTargets();
            var first = new WeatherSchedule(catalog, new WeatherSeed(123UL, 7UL), WeatherStateIds.Clear);
            var second = new WeatherSchedule(catalog, new WeatherSeed(123UL, 7UL), WeatherStateIds.Clear);

            double[] steps = { 1d, 78.5d, 1000d, 2500d, 42d };
            for (int index = 0; index < steps.Length; index++)
            {
                first.Advance(steps[index]);
                second.Advance(steps[index]);
                AssertSnapshotsEqual(first.CaptureSnapshot(), second.CaptureSnapshot());
                Assert.That(first.CurrentState, Is.EqualTo(second.CurrentState));
            }
        }

        [Test]
        public void Schedule_OnlyTraversesDeclaredFrontEdges()
        {
            WeatherProfileCatalog catalog = WeatherProfileCatalog.CreateRemakeDesignTargets();
            var schedule = new WeatherSchedule(catalog, new WeatherSeed(9981UL, 5UL), WeatherStateIds.Clear);

            for (int index = 0; index < 64; index++)
            {
                WeatherFront front = schedule.CurrentFront;
                Assert.That(catalog.Get(front.From).AllowsSuccessor(front.To), Is.True);
                schedule.Advance(front.DurationSeconds);
            }
        }

        [Test]
        public void WeatherState_Lerp_InterpolatesNumericFrontOutputs()
        {
            WeatherProfileCatalog catalog = WeatherProfileCatalog.CreateRemakeDesignTargets();
            WeatherState clear = catalog.Get(WeatherStateIds.Clear).TargetState;
            WeatherState rain = catalog.Get(WeatherStateIds.SteadyRain).TargetState;

            WeatherState midpoint = WeatherState.Lerp(clear, rain, 0.5f);

            Assert.That(midpoint.CloudCoverage01, Is.EqualTo((clear.CloudCoverage01 + rain.CloudCoverage01) * 0.5f).Within(0.0001f));
            Assert.That(midpoint.PrecipitationIntensity01, Is.EqualTo(rain.PrecipitationIntensity01 * 0.5f).Within(0.0001f));
            Assert.That(midpoint.WindSpeedMetersPerSecond, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void Schedule_InvalidRestore_DoesNotMutateCurrentState()
        {
            WeatherProfileCatalog catalog = WeatherProfileCatalog.CreateRemakeDesignTargets();
            var schedule = new WeatherSchedule(catalog, new WeatherSeed(17UL), WeatherStateIds.Clear);
            schedule.Advance(123d);
            WeatherScheduleSnapshot before = schedule.CaptureSnapshot();
            var invalid = new WeatherScheduleSnapshot(
                before.ConfigId,
                new WeatherStateId("weather.unknown"),
                before.TargetProfileId,
                before.FrontDurationSeconds,
                before.TransitionDurationSeconds,
                before.ElapsedSeconds,
                before.Cursor,
                before.RandomState);

            Assert.Throws<ArgumentException>(() => schedule.Restore(invalid));
            AssertSnapshotsEqual(before, schedule.CaptureSnapshot());
        }

        private static void AssertSnapshotsEqual(WeatherScheduleSnapshot expected, WeatherScheduleSnapshot actual)
        {
            Assert.That(actual.ConfigId, Is.EqualTo(expected.ConfigId));
            Assert.That(actual.CurrentProfileId, Is.EqualTo(expected.CurrentProfileId));
            Assert.That(actual.TargetProfileId, Is.EqualTo(expected.TargetProfileId));
            Assert.That(actual.FrontDurationSeconds, Is.EqualTo(expected.FrontDurationSeconds));
            Assert.That(actual.TransitionDurationSeconds, Is.EqualTo(expected.TransitionDurationSeconds));
            Assert.That(actual.ElapsedSeconds, Is.EqualTo(expected.ElapsedSeconds));
            Assert.That(actual.Cursor, Is.EqualTo(expected.Cursor));
            Assert.That(actual.RandomState, Is.EqualTo(expected.RandomState));
        }
    }
}
