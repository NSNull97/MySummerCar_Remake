using System;
using System.Collections;
using System.Reflection;
using Enviro;
using MSC.Core.Time;
using MSC.Development.WeatherLab;
using MSC.Weather.Domain;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Lightning;
using MSC.Weather.Presentation;
using MSC.Weather.Wetness;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.Enviro3Integration
{
    public sealed class WeatherLabTimeDomainIntegrationTests
    {
        [UnityTest, Order(1)]
        public IEnumerator DevTimeCommands_AreBoundedPauseSafeAndSmoothAcrossScheduledFront()
        {
            EditorSceneManager.OpenScene(
                WeatherLabSceneMarker.SceneAssetPath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            yield return new EnterPlayMode();
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
            }

            var observations = new Observations();
            Exception runtimeException = null;
            try
            {
                WeatherLabStateController controller =
                    Object.FindFirstObjectByType<WeatherLabStateController>();
                EnviroManager manager = Object.FindFirstObjectByType<EnviroManager>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(manager, Is.Not.Null);

                controller.SetPaused(true);
                double beforeRejectedAdvance = controller.GameTime.CurrentGameTimeSeconds;
                bool rejected = !controller.TryAdvanceGameSeconds(
                    31d * 86400d,
                    out string rejectionFailure);
                observations.OversizedAdvanceRejectedAtomically =
                    rejected &&
                    !string.IsNullOrWhiteSpace(rejectionFailure) &&
                    controller.GameTime.Snapshot.IsPaused &&
                    Math.Abs(
                        controller.GameTime.CurrentGameTimeSeconds - beforeRejectedAdvance) < 0.000001d;

                bool accepted = controller.TryAdvanceGameSeconds(60d, out string advanceFailure);
                observations.ValidAdvanceRestoredPause =
                    accepted &&
                    string.IsNullOrEmpty(advanceFailure) &&
                    controller.GameTime.Snapshot.IsPaused &&
                    Math.Abs(
                        controller.GameTime.CurrentGameTimeSeconds -
                        beforeRejectedAdvance -
                        60d) < 0.0001d;

                bool nearMidnightAccepted = controller.TrySetDateAndTime(
                    controller.GameTime.Snapshot.Date,
                    86399.999d,
                    out string midnightFailure);
                observations.NearMidnightProjectionStayedOperational =
                    nearMidnightAccepted &&
                    string.IsNullOrEmpty(midnightFailure) &&
                    string.IsNullOrEmpty(controller.LastMappingFailure) &&
                    controller.Status.IsOperational;

                controller.SetTimeScale(3.5d);
                controller.ApplyPreset(EnvironmentPresentationPresetKind.Clear);
                EnviroWeatherType initialBinding = manager.Weather.targetWeatherType;
                observations.ManualOverrideRemoved = controller.RemoveManualWeatherOverride();
                RecordAutomaticBindingChangeIfAny(
                    controller,
                    manager,
                    initialBinding,
                    observations);
                for (int step = 0;
                     step < 120 && !observations.AutomaticBindingChangeObserved;
                     step++)
                {
                    if (!controller.TryAdvanceGameSeconds(30d, out string failure))
                    {
                        throw new InvalidOperationException(
                            "Scheduled-front test advance failed: " + failure);
                    }

                    RecordAutomaticBindingChangeIfAny(
                        controller,
                        manager,
                        initialBinding,
                        observations);
                }
            }
            catch (Exception exception)
            {
                runtimeException = exception;
            }

            yield return new ExitPlayMode();

            Assert.That(
                runtimeException,
                Is.Null,
                runtimeException == null ? string.Empty : runtimeException.ToString());
            Assert.That(observations.OversizedAdvanceRejectedAtomically, Is.True);
            Assert.That(observations.ValidAdvanceRestoredPause, Is.True);
            Assert.That(observations.NearMidnightProjectionStayedOperational, Is.True);
            Assert.That(observations.ManualOverrideRemoved, Is.True);
            Assert.That(observations.AutomaticBindingChangeObserved, Is.True);
            Assert.That(
                observations.AutomaticBindingChangeUsedSmoothPath,
                Is.True,
                $"instant={observations.InstantTransition}; " +
                $"clouds={observations.ActualAutomaticTransitionRate:R}; " +
                $"effects={observations.EffectsTransitionRate:R}; " +
                $"environment={observations.EnvironmentTransitionRate:R}");
            Assert.That(
                observations.ActualAutomaticTransitionRate,
                Is.EqualTo(observations.ExpectedAutomaticTransitionRate).Within(0.00001f),
                $"Expected {observations.ExpectedSimulationDurationSeconds:F6} real seconds " +
                "from the current GameTime config/rate before Enviro's exponential mapping.");
        }

        [UnityTest, Order(2)]
        public IEnumerator TrySetDateAndTime_DefaultDateFailsClosed()
        {
            EditorSceneManager.OpenScene(
                WeatherLabSceneMarker.SceneAssetPath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            yield return new EnterPlayMode();
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
            }

            bool rejected = false;
            string failure = string.Empty;
            Exception runtimeException = null;
            try
            {
                WeatherLabStateController controller =
                    Object.FindFirstObjectByType<WeatherLabStateController>();
                Assert.That(controller, Is.Not.Null);
                rejected = !controller.TrySetDateAndTime(
                    default,
                    12d * 60d * 60d,
                    out failure);
            }
            catch (Exception exception)
            {
                runtimeException = exception;
            }

            yield return new ExitPlayMode();

            Assert.That(
                runtimeException,
                Is.Null,
                runtimeException == null ? string.Empty : runtimeException.ToString());
            Assert.That(rejected, Is.True);
            Assert.That(failure, Is.Not.Empty);
        }

        [UnityTest, Order(3)]
        public IEnumerator DevAdvance_CallbackFailureRollsBackEveryAuthoritativeDomain()
        {
            EditorSceneManager.OpenScene(
                WeatherLabSceneMarker.SceneAssetPath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            yield return new EnterPlayMode();
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
            }

            bool rejectedWithoutPartialMutation = false;
            Exception runtimeException = null;
            try
            {
                WeatherLabStateController controller =
                    Object.FindFirstObjectByType<WeatherLabStateController>();
                Assert.That(controller, Is.Not.Null);
                controller.SetPaused(true);

                GameTimeState timeBefore = controller.GameTime.CaptureState();
                WeatherDirector weather =
                    GetPrivateField<WeatherDirector>(controller, "weatherDirector");
                GlobalWetnessController wetness =
                    GetPrivateField<GlobalWetnessController>(controller, "wetnessController");
                LightningStrikeDirector lightning =
                    GetPrivateField<LightningStrikeDirector>(controller, "lightningDirector");
                WeatherSnapshot weatherBefore = weather.CaptureSnapshot();
                WetnessSnapshot wetnessBefore = wetness.CaptureSnapshot();
                LightningDirectorSnapshot lightningBefore = lightning.CaptureSnapshot();

                using (controller.GameTime.Subscribe((in GameTimeEvent gameEvent) =>
                       {
                           if (gameEvent.Kind == GameTimeEventKind.Advanced)
                           {
                               throw new ApplicationException("intentional clock observer failure");
                           }
                       }))
                {
                    bool rejected = !controller.TryAdvanceGameSeconds(
                        60d,
                        out string failure);
                    rejectedWithoutPartialMutation =
                        rejected &&
                        !string.IsNullOrWhiteSpace(failure) &&
                        controller.GameTime.CaptureState().Equals(timeBefore) &&
                        weather.CaptureSnapshot().SimulationSeconds.Equals(
                            weatherBefore.SimulationSeconds) &&
                        wetness.CaptureSnapshot().State.Equals(wetnessBefore.State) &&
                        lightning.CaptureSnapshot().SimulationSeconds.Equals(
                            lightningBefore.SimulationSeconds) &&
                        controller.GameTime.Snapshot.IsPaused;
                }
            }
            catch (Exception exception)
            {
                runtimeException = exception;
            }

            yield return new ExitPlayMode();

            Assert.That(
                runtimeException,
                Is.Null,
                runtimeException == null ? string.Empty : runtimeException.ToString());
            Assert.That(rejectedWithoutPartialMutation, Is.True);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            return (T)field.GetValue(target);
        }

        private static void RecordAutomaticBindingChangeIfAny(
            WeatherLabStateController controller,
            EnviroManager manager,
            EnviroWeatherType initialBinding,
            Observations observations)
        {
            if (manager.Weather.targetWeatherType == initialBinding)
            {
                return;
            }

            observations.AutomaticBindingChangeObserved = true;
            WeatherTransition transition = controller.CurrentTimeline.Transition;
            double remainingGameSeconds = Math.Max(
                0d,
                transition.Front.TransitionDurationSeconds - transition.ElapsedSeconds);
            GameTimeService timeService =
                GetPrivateField<GameTimeService>(controller, "gameTime");
            double gameSecondsPerSimulationSecond =
                timeService.Config.GameTicksPerSimulationSecondAtScaleOne /
                (double)GameTimeConfig.TicksPerGameSecond *
                timeService.Snapshot.TimeScale;
            observations.ExpectedSimulationDurationSeconds =
                Math.Max(
                    0.25d,
                    remainingGameSeconds / gameSecondsPerSimulationSecond);
            observations.ExpectedAutomaticTransitionRate = Mathf.Clamp(
                4.60517019f / (float)observations.ExpectedSimulationDurationSeconds,
                0.0001f,
                1000f);
            observations.ActualAutomaticTransitionRate =
                manager.Weather.Settings.cloudsTransitionSpeed;
            observations.InstantTransition =
                GetPrivateField<bool>(manager.Weather, "instantTransition");
            observations.EffectsTransitionRate =
                manager.Weather.Settings.effectsTransitionSpeed;
            observations.EnvironmentTransitionRate =
                manager.Weather.Settings.environmentTransitionSpeed;
            observations.AutomaticBindingChangeUsedSmoothPath =
                !observations.InstantTransition &&
                Mathf.Approximately(
                    observations.EffectsTransitionRate,
                    observations.ActualAutomaticTransitionRate) &&
                Mathf.Approximately(
                    observations.EnvironmentTransitionRate,
                    observations.ActualAutomaticTransitionRate);
        }

        private sealed class Observations
        {
            public bool OversizedAdvanceRejectedAtomically { get; set; }

            public bool ValidAdvanceRestoredPause { get; set; }

            public bool NearMidnightProjectionStayedOperational { get; set; }

            public bool ManualOverrideRemoved { get; set; }

            public bool AutomaticBindingChangeObserved { get; set; }

            public bool AutomaticBindingChangeUsedSmoothPath { get; set; }

            public double ExpectedSimulationDurationSeconds { get; set; }

            public float ExpectedAutomaticTransitionRate { get; set; }

            public float ActualAutomaticTransitionRate { get; set; }

            public bool InstantTransition { get; set; }

            public float EffectsTransitionRate { get; set; }

            public float EnvironmentTransitionRate { get; set; }
        }
    }
}
