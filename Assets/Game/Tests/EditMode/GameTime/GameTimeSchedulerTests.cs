using System;
using System.Collections.Generic;
using MSC.Core.Time;
using NUnit.Framework;

namespace MSC.Tests.EditMode.GameTime
{
    public sealed class GameTimeSchedulerTests
    {
        [Test]
        public void DueEvents_FireByTickThenInsertionOrder_AndCancellationIsStable()
        {
            var service = new GameTimeService(GameTimeServiceTests.CreateConfig());
            var fired = new List<string>();

            service.ScheduleAtGameTick(
                2 * GameTimeConfig.TicksPerGameSecond,
                "same.first",
                (in GameTimeEvent gameEvent) => fired.Add(gameEvent.ScheduledEventId));
            service.ScheduleAtGameTick(
                GameTimeConfig.TicksPerGameSecond,
                "early",
                (in GameTimeEvent gameEvent) => fired.Add(gameEvent.ScheduledEventId));
            GameTimeScheduleHandle cancelled = service.ScheduleAtGameTick(
                2 * GameTimeConfig.TicksPerGameSecond,
                "same.cancelled",
                (in GameTimeEvent gameEvent) => fired.Add(gameEvent.ScheduledEventId));
            service.ScheduleAtGameTick(
                2 * GameTimeConfig.TicksPerGameSecond,
                "same.last",
                (in GameTimeEvent gameEvent) => fired.Add(gameEvent.ScheduledEventId));

            Assert.That(service.CancelScheduledEvent(cancelled), Is.True);
            Assert.That(service.CancelScheduledEvent(cancelled), Is.False);
            service.Advance(3d);

            Assert.That(fired, Is.EqualTo(new[] { "early", "same.first", "same.last" }));
            Assert.That(service.ScheduledEventCount, Is.Zero);
        }

        [Test]
        public void SchedulingAtCurrentOrPastTick_IsRejectedWithoutQueueMutation()
        {
            var service = new GameTimeService(GameTimeServiceTests.CreateConfig());
            service.Advance(1d);

            Assert.Throws<ArgumentOutOfRangeException>(() => service.ScheduleAtGameTick(
                service.CaptureState().ElapsedGameTicks,
                "invalid.current",
                Ignore));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.ScheduleAtGameTick(
                1,
                "invalid.past",
                Ignore));
            Assert.That(service.ScheduledEventCount, Is.Zero);
        }

        [Test]
        public void GlobalSubscribersObserveScheduledEventsInTheSameDeterministicOrder()
        {
            var service = new GameTimeService(GameTimeServiceTests.CreateConfig());
            var observed = new List<string>();
            using (service.Subscribe((in GameTimeEvent gameEvent) =>
                   {
                       if (gameEvent.IsScheduled)
                       {
                           observed.Add(gameEvent.ScheduledEventId);
                       }
                   }))
            {
                service.ScheduleAtGameTick(30, "third", Ignore);
                service.ScheduleAtGameTick(10, "first", Ignore);
                service.ScheduleAtGameTick(20, "second", Ignore);
                service.Advance(0.00004d);
            }

            Assert.That(observed, Is.EqualTo(new[] { "first", "second", "third" }));
        }

        private static void Ignore(in GameTimeEvent _)
        {
        }
    }
}
