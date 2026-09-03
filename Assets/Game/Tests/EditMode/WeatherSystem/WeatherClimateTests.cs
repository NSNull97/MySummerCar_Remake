using System;
using System.Collections.Generic;
using MSC.Weather.System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DomainWeatherProfileCatalog = MSC.Weather.Domain.WeatherProfileCatalog;
using DomainWeatherDirector = MSC.Weather.Domain.WeatherDirector;
using DomainWeatherSchedule = MSC.Weather.Domain.WeatherSchedule;
using DomainWeatherScheduleContext = MSC.Weather.Domain.WeatherScheduleContext;
using DomainWeatherScheduleSnapshot = MSC.Weather.Domain.WeatherScheduleSnapshot;
using DomainWeatherSeed = MSC.Weather.Domain.WeatherSeed;
using DomainWeatherStateId = MSC.Weather.Domain.WeatherStateId;
using DomainWeatherStateIds = MSC.Weather.Domain.WeatherStateIds;

namespace MSC.Tests.EditMode.WeatherSystem
{
    public sealed class WeatherClimateTests
    {
        [Test]
        public void Defaults_DefineExactlySixteenUniqueStates()
        {
            WeatherClimateCatalog catalog =
                FinnishSummerClimateDefaults.CreateCatalog();
            Assert.That(catalog.Presets.Count, Is.EqualTo(16));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Presets.Count; index++)
            {
                Assert.That(ids.Add(catalog.Presets[index].StableId), Is.True);
            }
        }

        [Test]
        public void GeneratedClimateProfile_ContainsSixteenValidPresetAssets()
        {
            const string path =
                "Assets/Game/Weather/System/Content/FinnishSummer/FinnishSummerClimateProfile.asset";
            FinnishSummerClimateProfile profile =
                AssetDatabase.LoadAssetAtPath<FinnishSummerClimateProfile>(path);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.Presets.Count, Is.EqualTo(16));
            Assert.That(profile.CreateCatalog().Presets.Count, Is.EqualTo(16));
        }

        [Test]
        public void ProductionProjection_ContainsSixteenStableStates()
        {
            DomainWeatherProfileCatalog catalog =
                FinnishSummerProductionCatalogAdapter.Create(
                    FinnishSummerClimateDefaults.CreateCatalog(),
                    out DomainWeatherStateId initialStateId);

            Assert.That(catalog.Profiles.Count, Is.EqualTo(16));
            Assert.That(initialStateId, Is.EqualTo(DomainWeatherStateIds.PartlyCloudy));
            Assert.That(DomainWeatherStateIds.All.Count, Is.EqualTo(9));
            Assert.That(DomainWeatherStateIds.AllKnown.Count, Is.EqualTo(16));
            for (int index = 0; index < DomainWeatherStateIds.AllKnown.Count; index++)
            {
                Assert.That(catalog.Contains(DomainWeatherStateIds.AllKnown[index]), Is.True);
            }
        }

        [Test]
        public void ProductionProjection_RestoresLegacyFrontWithoutSchedulingItAgain()
        {
            DomainWeatherProfileCatalog catalog =
                FinnishSummerProductionCatalogAdapter.Create(
                    FinnishSummerClimateDefaults.CreateCatalog(),
                    out DomainWeatherStateId initialStateId);
            var schedule = new DomainWeatherSchedule(
                catalog,
                new DomainWeatherSeed(42UL, 9UL),
                initialStateId);
            DomainWeatherScheduleSnapshot baseline = schedule.CaptureSnapshot();
            var legacyFront = new DomainWeatherScheduleSnapshot(
                DomainWeatherProfileCatalog.RemakeDesignTargetConfigId,
                DomainWeatherStateIds.Clear,
                DomainWeatherStateIds.MorningMist,
                1200d,
                300d,
                150d,
                baseline.Cursor,
                baseline.RandomState);

            Assert.DoesNotThrow(() => schedule.Restore(legacyFront));
            Assert.That(schedule.CurrentFront.From, Is.EqualTo(DomainWeatherStateIds.Clear));
            Assert.That(schedule.CurrentFront.To, Is.EqualTo(DomainWeatherStateIds.MorningMist));
            Assert.That(
                catalog.Get(DomainWeatherStateIds.Clear)
                    .AllowsSuccessor(DomainWeatherStateIds.MorningMist),
                Is.False,
                "The legacy edge is restore-only and must not enter new scheduling.");

            var forbiddenFront = new DomainWeatherScheduleSnapshot(
                DomainWeatherProfileCatalog.RemakeDesignTargetConfigId,
                DomainWeatherStateIds.Clear,
                DomainWeatherStateIds.HeavyRain,
                1200d,
                300d,
                150d,
                baseline.Cursor,
                baseline.RandomState);
            Assert.Throws<ArgumentException>(() => schedule.Restore(forbiddenFront));
        }

        [Test]
        public void ProductionProjection_UsesTimeClimateAndHistoryPolicy()
        {
            DomainWeatherProfileCatalog catalog =
                FinnishSummerProductionCatalogAdapter.Create(
                    FinnishSummerClimateDefaults.CreateCatalog(),
                    out DomainWeatherStateId initialStateId);
            var noon = new DomainWeatherScheduleContext(8, 0.5f, 15f, 0.62f);
            var schedule = new DomainWeatherSchedule(
                catalog,
                new DomainWeatherSeed(19950801UL, 7UL),
                initialStateId,
                noon);

            Assert.That(catalog.ClimateAwareScheduling, Is.True);
            Assert.That(catalog.HistoryLength, Is.EqualTo(6));
            Assert.That(
                catalog.Get(DomainWeatherStateIds.MorningMist)
                    .Selection.AllowsTime(noon.NormalizedTimeOfDay01),
                Is.False);
            Assert.That(
                catalog.Get(DomainWeatherStateIds.BlueHour)
                    .Selection.AllowsTime(noon.NormalizedTimeOfDay01),
                Is.False);
            Assert.That(
                schedule.CurrentFront.To,
                Is.Not.EqualTo(DomainWeatherStateIds.MorningMist));
            Assert.That(
                schedule.CurrentFront.To,
                Is.Not.EqualTo(DomainWeatherStateIds.BlueHour));
        }

        [Test]
        public void ProductionProjection_SaveHistoryContinuesWeightedSequence()
        {
            DomainWeatherProfileCatalog catalog =
                FinnishSummerProductionCatalogAdapter.Create(
                    FinnishSummerClimateDefaults.CreateCatalog(),
                    out DomainWeatherStateId initialStateId);
            var context = new DomainWeatherScheduleContext(
                8,
                0.82f,
                9f,
                0.78f);
            var uninterrupted = new DomainWeatherSchedule(
                catalog,
                new DomainWeatherSeed(887766UL, 7UL),
                initialStateId,
                context);
            uninterrupted.Advance(7200d, context);
            DomainWeatherScheduleSnapshot snapshot =
                uninterrupted.CaptureSnapshot();
            var restored = new DomainWeatherSchedule(
                catalog,
                new DomainWeatherSeed(1UL, 7UL),
                initialStateId,
                context);
            restored.Restore(snapshot);

            Assert.That(snapshot.RecentProfileIds.Length, Is.EqualTo(6));
            Assert.That(snapshot.HistoryCount, Is.GreaterThan(0));
            for (int index = 0; index < 40; index++)
            {
                double step = 173d + index;
                uninterrupted.Advance(step, context);
                restored.Advance(step, context);
                DomainWeatherScheduleSnapshot expected =
                    uninterrupted.CaptureSnapshot();
                DomainWeatherScheduleSnapshot actual =
                    restored.CaptureSnapshot();
                Assert.That(actual.CurrentProfileId, Is.EqualTo(expected.CurrentProfileId));
                Assert.That(actual.TargetProfileId, Is.EqualTo(expected.TargetProfileId));
                Assert.That(actual.RandomState, Is.EqualTo(expected.RandomState));
                Assert.That(actual.HistoryCount, Is.EqualTo(expected.HistoryCount));
                Assert.That(actual.HistoryWriteIndex, Is.EqualTo(expected.HistoryWriteIndex));
                Assert.That(actual.RecentProfileIds, Is.EqualTo(expected.RecentProfileIds));
            }
        }

        [Test]
        public void ProductionDirector_SteadyAdvanceDoesNotAllocateManagedMemory()
        {
            DomainWeatherProfileCatalog catalog =
                FinnishSummerProductionCatalogAdapter.Create(
                    FinnishSummerClimateDefaults.CreateCatalog(),
                    out DomainWeatherStateId initialStateId);
            var context = new DomainWeatherScheduleContext(8, 0.5f, 15f, 0.62f);
            var director = new DomainWeatherDirector(
                catalog,
                new DomainWeatherSeed(123UL, 7UL),
                initialStateId,
                context);
            for (int index = 0; index < 64; index++)
            {
                director.Advance(0.1d, context);
                _ = director.CurrentState;
            }

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 2000; index++)
            {
                director.Advance(0.1d, context);
                _ = director.CurrentState;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0L));
        }

        [Test]
        public void Graph_RejectsForbiddenInstantFronts()
        {
            WeatherClimateCatalog catalog =
                FinnishSummerClimateDefaults.CreateCatalog();
            Assert.That(
                catalog.Get(FinnishSummerWeatherIds.ClearCool)
                    .AllowsSuccessor(FinnishSummerWeatherIds.HeavyRain),
                Is.False);
            Assert.That(
                catalog.Get(FinnishSummerWeatherIds.HeavyRain)
                    .AllowsSuccessor(FinnishSummerWeatherIds.ClearCool),
                Is.False);
            Assert.That(
                catalog.Get(FinnishSummerWeatherIds.LakeMist)
                    .AllowsSuccessor(FinnishSummerWeatherIds.ClearCool),
                Is.False);
            Assert.That(
                catalog.Get(FinnishSummerWeatherIds.MorningMist)
                    .AllowsSuccessor(FinnishSummerWeatherIds.ClearCool),
                Is.False);
        }

        [Test]
        public void LakeMist_HasDenseFogVisibilityTarget()
        {
            WeatherClimateCatalog catalog =
                FinnishSummerClimateDefaults.CreateCatalog();
            WeatherState lakeMist = catalog
                .Get(FinnishSummerWeatherIds.LakeMist)
                .TargetState;
            WeatherState morningMist = catalog
                .Get(FinnishSummerWeatherIds.MorningMist)
                .TargetState;

            Assert.That(lakeMist.FogDistanceMeters, Is.EqualTo(80f));
            Assert.That(lakeMist.FogDistanceMeters,
                Is.LessThan(morningMist.FogDistanceMeters));
        }

        [Test]
        public void Storm_IsReachableOnlyFromHeavyRain()
        {
            WeatherClimateCatalog catalog =
                FinnishSummerClimateDefaults.CreateCatalog();
            int predecessors = 0;
            string predecessor = string.Empty;
            for (int index = 0; index < catalog.Presets.Count; index++)
            {
                WeatherPresetDefinition preset = catalog.Presets[index];
                if (preset.AllowsSuccessor(
                        FinnishSummerWeatherIds.RareThunderstorm))
                {
                    predecessors++;
                    predecessor = preset.StableId;
                }
            }

            Assert.That(predecessors, Is.EqualTo(1));
            Assert.That(predecessor, Is.EqualTo(FinnishSummerWeatherIds.HeavyRain));
        }

        [Test]
        public void Scheduler_IsDeterministicForSameSeedAndContext()
        {
            WeatherClimateCatalog catalog =
                FinnishSummerClimateDefaults.CreateCatalog();
            WeatherClimateContext context =
                WeatherClimateContext.FinnishSummerDefault;
            var left = new WeatherScheduler(catalog, 19950801UL, 19UL, context);
            var right = new WeatherScheduler(catalog, 19950801UL, 19UL, context);

            for (int index = 0; index < 128; index++)
            {
                left.Advance(137d, context);
                right.Advance(137d, context);
                Assert.That(left.CurrentPresetId, Is.EqualTo(right.CurrentPresetId));
                Assert.That(left.TargetPresetId, Is.EqualTo(right.TargetPresetId));
                Assert.That(left.CurrentState, Is.EqualTo(right.CurrentState));
            }
        }

        [Test]
        public void Scheduler_RestoreContinuesSameRandomSequence()
        {
            WeatherClimateCatalog catalog =
                FinnishSummerClimateDefaults.CreateCatalog();
            WeatherClimateContext context =
                WeatherClimateContext.FinnishSummerDefault;
            var uninterrupted = new WeatherScheduler(
                catalog,
                887766UL,
                29UL,
                context);
            uninterrupted.Advance(4700d, context);
            WeatherSchedulerSnapshot snapshot = uninterrupted.CaptureSnapshot();
            var restored = new WeatherScheduler(catalog, 1UL, 29UL, context);
            restored.Restore(snapshot);

            for (int index = 0; index < 80; index++)
            {
                uninterrupted.Advance(211d, context);
                restored.Advance(211d, context);
                Assert.That(
                    restored.CaptureSnapshot().TargetPresetId,
                    Is.EqualTo(uninterrupted.CaptureSnapshot().TargetPresetId));
                Assert.That(restored.CurrentState, Is.EqualTo(uninterrupted.CurrentState));
            }
        }

        [Test]
        public void Scheduler_SteadyStateAdvanceDoesNotAllocateManagedMemory()
        {
            WeatherClimateCatalog catalog =
                FinnishSummerClimateDefaults.CreateCatalog();
            WeatherClimateContext context =
                WeatherClimateContext.FinnishSummerDefault;
            var scheduler = new WeatherScheduler(catalog, 123UL, 17UL, context);
            for (int index = 0; index < 64; index++)
            {
                scheduler.Advance(0.1d, context);
                _ = scheduler.CurrentState;
            }

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 2000; index++)
            {
                scheduler.Advance(0.1d, context);
                _ = scheduler.CurrentState;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0L));
        }

        [Test]
        public void Transition_BlendsEveryContinuousChannel()
        {
            WeatherState from = CreateState(0.1f, new Vector2(1f, 0f));
            WeatherState to = CreateState(0.9f, new Vector2(0f, 1f));
            WeatherState midpoint = WeatherTransitionController.Blend(
                from,
                to,
                0.5f,
                new WeatherTransitionCurves());

            Assert.That(midpoint.CloudCoverage01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.CloudDensity01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.CloudErosion01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.CloudShadowStrength01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.Precipitation01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.Drizzle01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.ThunderIntensity01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.FogDensity01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.FogDistanceMeters, Is.InRange(1000f, 9000f));
            Assert.That(midpoint.AtmosphericHaze01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.WindSpeedMetersPerSecond, Is.InRange(1f, 9f));
            Assert.That(midpoint.WindDirectionXZ.x, Is.GreaterThan(0f));
            Assert.That(midpoint.WindDirectionXZ.y, Is.GreaterThan(0f));
            Assert.That(midpoint.WindGustiness01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.SurfaceWetness01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.PuddleAmount01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.SunVisibility01, Is.InRange(0.1f, 0.9f));
            Assert.That(midpoint.SkyBrightnessMultiplier, Is.InRange(0.2f, 1.8f));
            Assert.That(midpoint.AmbientLightMultiplier, Is.InRange(0.2f, 1.8f));
            Assert.That(midpoint.TemperatureCelsius, Is.InRange(2f, 20f));
            Assert.That(midpoint.Humidity01, Is.InRange(0.1f, 0.9f));
        }

        [Test]
        public void Wetness_PersistsAfterRainAndThenDries()
        {
            var wetness = new SurfaceWetnessController(
                new SurfaceWetnessConfig(0.01f, 0.004f, 0.001f, 0.0005f, 0.2f));
            WeatherState rain = CreateState(0.9f, Vector2.up);
            wetness.Advance(60d, rain, 1f);
            float afterRain = wetness.Wetness01;
            WeatherState dry = CreateState(0f, Vector2.up);
            wetness.Advance(1d, dry, 1f);
            Assert.That(wetness.Wetness01, Is.GreaterThan(0f));
            Assert.That(wetness.Wetness01, Is.LessThan(afterRain));
            wetness.Advance(2000d, dry, 2f);
            Assert.That(wetness.Wetness01, Is.LessThan(afterRain * 0.5f));
        }

        [Test]
        public void SaveJson_RestoresMidFrontExactly()
        {
            WeatherClimateCatalog catalog =
                FinnishSummerClimateDefaults.CreateCatalog();
            WeatherClimateContext context =
                WeatherClimateContext.FinnishSummerDefault;
            var source = new WeatherDirector(catalog, 44UL, context);
            source.Advance(1700d, context);
            string json = new WeatherSaveController(source).CaptureJson();
            var destination = new WeatherDirector(catalog, 999UL, context);

            new WeatherSaveController(destination).RestoreJson(json);

            Assert.That(destination.CurrentState, Is.EqualTo(source.CurrentState));
            Assert.That(destination.TargetPresetId, Is.EqualTo(source.TargetPresetId));
            Assert.That(
                destination.TransitionProgress01,
                Is.EqualTo(source.TransitionProgress01).Within(0.000001f));
            destination.Advance(500d, context);
            source.Advance(500d, context);
            Assert.That(destination.CurrentState, Is.EqualTo(source.CurrentState));

            WeatherDirectorSnapshot corrupted = destination.CaptureSnapshot();
            corrupted.GlobalState = ReplaceCloudDensity(
                corrupted.GlobalState,
                corrupted.GlobalState.CloudDensity01 > 0.5f
                    ? corrupted.GlobalState.CloudDensity01 - 0.1f
                    : corrupted.GlobalState.CloudDensity01 + 0.1f);
            WeatherState checkpoint = destination.CurrentState;
            Assert.Throws<ArgumentException>(
                () => new WeatherSaveController(destination).Restore(corrupted));
            Assert.That(destination.CurrentState, Is.EqualTo(checkpoint));
        }

        private static WeatherState ReplaceCloudDensity(
            in WeatherState state,
            float cloudDensity01) => new WeatherState(
            state.CloudCoverage01,
            cloudDensity01,
            state.CloudErosion01,
            state.CloudShadowStrength01,
            state.Precipitation01,
            state.Drizzle01,
            state.ThunderIntensity01,
            state.FogDensity01,
            state.FogDistanceMeters,
            state.AtmosphericHaze01,
            state.WindSpeedMetersPerSecond,
            state.WindDirectionXZ,
            state.WindGustiness01,
            state.SurfaceWetness01,
            state.PuddleAmount01,
            state.SunVisibility01,
            state.SkyBrightnessMultiplier,
            state.AmbientLightMultiplier,
            state.TemperatureCelsius,
            state.Humidity01);

        private static WeatherState CreateState(
            float value,
            Vector2 windDirection)
        {
            float inverse = 1f - value;
            return new WeatherState(
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                value,
                Mathf.Lerp(1000f, 9000f, value),
                value,
                Mathf.Lerp(1f, 9f, value),
                windDirection,
                value,
                value,
                value,
                inverse,
                Mathf.Lerp(0.2f, 1.8f, value),
                Mathf.Lerp(0.2f, 1.8f, value),
                Mathf.Lerp(2f, 20f, value),
                value);
        }
    }
}
