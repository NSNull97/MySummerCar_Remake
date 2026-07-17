using System;
using System.Collections.Generic;
using MSC.Core.Time;
using NUnit.Framework;

namespace MSC.Tests.EditMode.GameTime
{
    public sealed class GameTimeServiceTests
    {
        [Test]
        public void Advance_UsesConfiguredDayLengthAndScaleDeterministically()
        {
            GameTimeConfig config = CreateConfig(
                dayLengthSimulationSeconds: 120d,
                defaultTimeScale: 1.5d);
            var first = new GameTimeService(config);
            var second = new GameTimeService(config);

            first.Advance(0.25d);
            first.Advance(0.75d);
            first.Advance(1d);
            second.Advance(0.25d);
            second.Advance(0.75d);
            second.Advance(1d);

            Assert.That(first.CurrentGameTimeSeconds, Is.EqualTo(2_160d).Within(0.000001d));
            Assert.That(first.CaptureState(), Is.EqualTo(second.CaptureState()));
            Assert.That(first.Snapshot.TimeOfDayTicks, Is.EqualTo(second.Snapshot.TimeOfDayTicks));
            Assert.That(first.Snapshot.NormalizedTimeOfDay01, Is.EqualTo(0.025d).Within(1e-12d));
        }

        [Test]
        public void PauseAndScale_ChangeOnlyExplicitProgression()
        {
            var service = new GameTimeService(CreateConfig());

            service.Advance(1d);
            Assert.That(service.CurrentGameTimeSeconds, Is.EqualTo(1d).Within(1e-9d));

            service.SetPaused(true);
            service.SetTimeScale(2d);
            service.Advance(100d);
            Assert.That(service.CurrentGameTimeSeconds, Is.EqualTo(1d).Within(1e-9d));

            service.SetPaused(false);
            service.Advance(2d);
            Assert.That(service.CurrentGameTimeSeconds, Is.EqualTo(5d).Within(1e-9d));
            Assert.That(service.Snapshot.TimeScale, Is.EqualTo(2d));
            Assert.That(service.Snapshot.IsPaused, Is.False);
        }

        [Test]
        public void Advance_AcrossMonthBoundary_UpdatesDateDayIndexAndEventOrder()
        {
            GameTimeConfig config = CreateConfig(
                startDate: new GameDate(1995, 1, 31),
                startTimeSeconds: 86_390d);
            var service = new GameTimeService(config);
            var eventKinds = new List<GameTimeEventKind>();
            using (service.Subscribe((in GameTimeEvent gameEvent) => eventKinds.Add(gameEvent.Kind)))
            {
                service.Advance(20d);
            }

            Assert.That(service.Snapshot.Date, Is.EqualTo(new GameDate(1995, 2, 1)));
            Assert.That(service.Snapshot.DayIndex, Is.EqualTo(1));
            Assert.That(service.Snapshot.SecondsOfDay, Is.EqualTo(10d).Within(1e-9d));
            Assert.That(
                eventKinds,
                Is.EqualTo(new[] { GameTimeEventKind.DayChanged, GameTimeEventKind.Advanced }));
        }

        [Test]
        public void InvalidDeltaAndScale_AreRejectedWithoutMutation()
        {
            var service = new GameTimeService(CreateConfig());
            service.Advance(2d);
            GameTimeState before = service.CaptureState();

            Assert.Throws<ArgumentOutOfRangeException>(() => service.Advance(-0.01d));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.Advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.Advance(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetTimeScale(0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetTimeScale(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => service.SetTimeScale(GameTimeConfig.MaximumTimeScale + 1d));

            Assert.That(service.CaptureState(), Is.EqualTo(before));
        }

        [Test]
        public void InvalidConfigValues_AreRejectedAndDefaultsAreExplicitDesignTargets()
        {
            Assert.That(
                GameTimeConfig.RemakeDesignTargetDefaults.TuningClassification,
                Is.EqualTo(GameTimeTuningClassification.RemakeDesignTarget));

            Assert.Throws<ArgumentException>(() => new GameTimeConfig(
                "Invalid ID",
                new GameDate(1995, 1, 1),
                0d,
                1d,
                1d,
                0.25d,
                0.75d,
                GameTimeTuningClassification.RemakeDesignTarget));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameTimeConfig(
                "time.invalid-day-length",
                new GameDate(1995, 1, 1),
                0d,
                double.NaN,
                1d,
                0.25d,
                0.75d,
                GameTimeTuningClassification.RemakeDesignTarget));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameTimeConfig(
                "time.non-finite-derived-rate",
                new GameDate(1995, 1, 1),
                0d,
                double.Epsilon,
                1d,
                0.25d,
                0.75d,
                GameTimeTuningClassification.RemakeDesignTarget));
            Assert.Throws<ArgumentException>(() => new GameTimeConfig(
                "time.invalid-sun",
                new GameDate(1995, 1, 1),
                0d,
                1d,
                1d,
                0.8d,
                0.2d,
                GameTimeTuningClassification.RemakeDesignTarget));
        }

        [Test]
        public void DisposedSubscription_DoesNotReceiveCurrentOrFutureEvents()
        {
            var service = new GameTimeService(CreateConfig());
            var calls = new List<string>();
            IDisposable secondSubscription = null;
            using (service.Subscribe((in GameTimeEvent _) =>
                   {
                       calls.Add("first");
                       secondSubscription.Dispose();
                   }))
            {
                secondSubscription = service.Subscribe(
                    (in GameTimeEvent _) => calls.Add("second"));
                service.Advance(1d);
                service.Advance(1d);
            }

            Assert.That(calls, Is.EqualTo(new[] { "first", "first" }));
        }

        [Test]
        public void SubscriberNestedMutations_FailFastWithoutChangingOuterStateOrEventOrder()
        {
            var service = new GameTimeService(CreateConfig());
            GameTimeSaveDto restoreDto = service.CaptureDto();
            var eventKinds = new List<GameTimeEventKind>();
            using (service.Subscribe((in GameTimeEvent gameEvent) =>
                   {
                       eventKinds.Add(gameEvent.Kind);
                       GameTimeState beforeNestedCalls = service.CaptureState();

                       Assert.Throws<InvalidOperationException>(() => service.Advance(1d));
                       Assert.Throws<InvalidOperationException>(() => service.SetPaused(true));
                       Assert.Throws<InvalidOperationException>(() => service.SetTimeScale(2d));
                       Assert.Throws<InvalidOperationException>(
                           () => service.TryRestoreDto(restoreDto, out _));

                       Assert.That(service.CaptureState(), Is.EqualTo(beforeNestedCalls));
                   }))
            {
                service.Advance(1d);
            }

            Assert.That(eventKinds, Is.EqualTo(new[] { GameTimeEventKind.Advanced }));
            Assert.That(service.CurrentGameTimeSeconds, Is.EqualTo(1d).Within(1e-9d));
            Assert.That(service.Snapshot.IsPaused, Is.False);
            Assert.That(service.Snapshot.TimeScale, Is.EqualTo(1d));
            Assert.That(service.ScheduledEventCount, Is.Zero);
        }

        [Test]
        public void EveryMutationNotification_RejectsReentrantAdvanceAndReleasesGuard()
        {
            AssertNotificationRejectsReentrantAdvance(
                service => service.Advance(1d),
                GameTimeEventKind.Advanced);
            AssertNotificationRejectsReentrantAdvance(
                service => service.SetPaused(true),
                GameTimeEventKind.PauseChanged);
            AssertNotificationRejectsReentrantAdvance(
                service => service.SetTimeScale(2d),
                GameTimeEventKind.TimeScaleChanged);
            AssertNotificationRejectsReentrantAdvance(
                service =>
                {
                    GameTimeSaveDto dto = service.CaptureDto();
                    Assert.That(service.TryRestoreDto(dto, out string failure), Is.True, failure);
                },
                GameTimeEventKind.StateRestored);
        }

        [Test]
        public void ScheduledCallbackNestedMutation_FailsFastAndAdvanceCompletes()
        {
            var service = new GameTimeService(CreateConfig());
            int scheduledCallbacks = 0;
            service.ScheduleAtGameTick(
                1,
                "time.tests.reentrant",
                (in GameTimeEvent _) =>
                {
                    scheduledCallbacks++;
                    GameTimeState beforeNestedCall = service.CaptureState();
                    Assert.Throws<InvalidOperationException>(() => service.SetPaused(true));
                    Assert.That(service.CaptureState(), Is.EqualTo(beforeNestedCall));
                });

            service.Advance(0.000001d);

            Assert.That(scheduledCallbacks, Is.EqualTo(1));
            Assert.That(service.CaptureState().ElapsedGameTicks, Is.EqualTo(1));
            Assert.That(service.Snapshot.IsPaused, Is.False);
            service.SetPaused(true);
            Assert.That(service.Snapshot.IsPaused, Is.True);
        }

        [Test]
        public void SubscriberFailure_RollsBackClockStateAndReleasesMutationGuard()
        {
            var service = new GameTimeService(CreateConfig());
            GameTimeState before = service.CaptureState();
            GameTimeSnapshot beforeSnapshot = service.Snapshot;
            IDisposable subscription = service.Subscribe(
                (in GameTimeEvent _) => throw new ApplicationException("observer failure"));

            GameTimeNotificationException exception = Assert.Throws<GameTimeNotificationException>(
                () => service.Advance(5d));

            Assert.That(exception.EventKind, Is.EqualTo(GameTimeEventKind.Advanced));
            Assert.That(exception.InnerException, Is.TypeOf<ApplicationException>());
            Assert.That(service.CaptureState(), Is.EqualTo(before));
            Assert.That(service.Snapshot, Is.EqualTo(beforeSnapshot));

            subscription.Dispose();
            service.Advance(1d);
            Assert.That(service.CurrentGameTimeSeconds, Is.EqualTo(1d).Within(1e-9d));
        }

        [Test]
        public void ScheduledCallbackFailure_RollsBackClockAndRestoresDueQueue()
        {
            var service = new GameTimeService(CreateConfig());
            GameTimeState before = service.CaptureState();
            GameTimeScheduleHandle handle = service.ScheduleAtGameTick(
                1,
                "time.tests.throwing-scheduled",
                (in GameTimeEvent _) => throw new ApplicationException("scheduled failure"));

            GameTimeNotificationException exception = Assert.Throws<GameTimeNotificationException>(
                () => service.Advance(0.000001d));

            Assert.That(exception.EventKind, Is.EqualTo(GameTimeEventKind.Scheduled));
            Assert.That(exception.EventId, Is.EqualTo("time.tests.throwing-scheduled"));
            Assert.That(service.CaptureState(), Is.EqualTo(before));
            Assert.That(service.ScheduledEventCount, Is.EqualTo(1));
            Assert.That(service.CancelScheduledEvent(handle), Is.True);
        }

        [Test]
        public void ObserverCannotMutateFutureSchedulerQueueReentrantly()
        {
            var service = new GameTimeService(CreateConfig());
            GameTimeScheduleHandle existing = service.ScheduleAtGameTick(
                100,
                "time.tests.future",
                (in GameTimeEvent _) => { });
            using (service.Subscribe((in GameTimeEvent gameEvent) =>
                   {
                       if (gameEvent.Kind != GameTimeEventKind.Advanced)
                       {
                           return;
                       }

                       Assert.Throws<InvalidOperationException>(() =>
                           service.ScheduleAtGameTick(
                               200,
                               "time.tests.reentrant-add",
                               (in GameTimeEvent _) => { }));
                       Assert.Throws<InvalidOperationException>(() =>
                           service.CancelScheduledEvent(existing));
                   }))
            {
                service.Advance(0.000001d);
            }

            Assert.That(service.ScheduledEventCount, Is.EqualTo(1));
            Assert.That(service.CancelScheduledEvent(existing), Is.True);
        }

        [Test]
        public void AdvanceWhileRetainingPause_IsSinglePausedClockMutation()
        {
            var service = new GameTimeService(CreateConfig());
            service.SetPaused(true);
            var events = new List<GameTimeEventKind>();
            using (service.Subscribe((in GameTimeEvent gameEvent) => events.Add(gameEvent.Kind)))
            {
                service.AdvanceWhileRetainingPause(30d);
            }

            Assert.That(service.CurrentGameTimeSeconds, Is.EqualTo(30d).Within(1e-9d));
            Assert.That(service.Snapshot.IsPaused, Is.True);
            Assert.That(events, Is.EqualTo(new[] { GameTimeEventKind.Advanced }));
        }

        private static void AssertNotificationRejectsReentrantAdvance(
            Action<GameTimeService> outerMutation,
            GameTimeEventKind expectedEventKind)
        {
            var service = new GameTimeService(CreateConfig());
            int notificationCount = 0;
            using (service.Subscribe((in GameTimeEvent gameEvent) =>
                   {
                       notificationCount++;
                       Assert.That(gameEvent.Kind, Is.EqualTo(expectedEventKind));
                       GameTimeState beforeNestedCall = service.CaptureState();
                       Assert.Throws<InvalidOperationException>(() => service.Advance(1d));
                       Assert.That(service.CaptureState(), Is.EqualTo(beforeNestedCall));
                   }))
            {
                outerMutation(service);
            }

            Assert.That(notificationCount, Is.EqualTo(1));
            service.SetTimeScale(3d);
            Assert.That(service.Snapshot.TimeScale, Is.EqualTo(3d));
        }

        internal static GameTimeConfig CreateConfig(
            GameDate? startDate = null,
            double startTimeSeconds = 0d,
            double dayLengthSimulationSeconds = 86_400d,
            double defaultTimeScale = 1d)
        {
            return new GameTimeConfig(
                "time.tests.v1",
                startDate ?? new GameDate(1995, 1, 1),
                startTimeSeconds,
                dayLengthSimulationSeconds,
                defaultTimeScale,
                0.25d,
                0.75d,
                GameTimeTuningClassification.RemakeDesignTarget);
        }
    }
}
