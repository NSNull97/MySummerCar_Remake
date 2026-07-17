using MSC.Core.Time;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.GameTime
{
    public sealed class GameTimeSaveDtoTests
    {
        [Test]
        public void JsonRoundTrip_RestoresExactTicksRemainderScalePauseAndCalendar()
        {
            GameTimeConfig config = GameTimeServiceTests.CreateConfig(
                startDate: new GameDate(1995, 3, 10),
                startTimeSeconds: 43_200d);
            var service = new GameTimeService(config);
            service.Advance(0.0000005d);
            service.SetTimeScale(1.25d);
            service.SetPaused(true);
            GameTimeState expectedState = service.CaptureState();
            GameTimeSaveDto captured = service.CaptureDto();

            string json = JsonUtility.ToJson(captured);
            GameTimeSaveDto restoredDto = JsonUtility.FromJson<GameTimeSaveDto>(json);

            service.SetPaused(false);
            service.SetTimeScale(2d);
            service.Advance(10d);
            Assert.That(service.TryRestoreDto(restoredDto, out string failure), Is.True, failure);

            Assert.That(service.CaptureState(), Is.EqualTo(expectedState));
            Assert.That(service.Snapshot.Date, Is.EqualTo(new GameDate(1995, 3, 10)));
            Assert.That(service.Snapshot.DayIndex, Is.Zero);
            Assert.That(service.Snapshot.TimeOfDayTicks, Is.EqualTo(43_200L * GameTimeConfig.TicksPerGameSecond));
            Assert.That(service.Snapshot.FractionalGameTickRemainder, Is.EqualTo(0.5d));
        }

        [Test]
        public void InvalidDto_DoesNotMutateClockOrClearScheduledEvents()
        {
            var service = new GameTimeService(GameTimeServiceTests.CreateConfig());
            service.Advance(1d);
            GameTimeState before = service.CaptureState();
            bool scheduledEventFired = false;
            service.ScheduleAtGameTick(
                before.ElapsedGameTicks + 10,
                "still.pending",
                (in GameTimeEvent _) => scheduledEventFired = true);
            GameTimeSaveDto invalid = service.CaptureDto();
            invalid.schemaVersion++;

            Assert.That(service.TryRestoreDto(invalid, out string failure), Is.False);
            Assert.That(failure, Is.Not.Empty);
            Assert.That(service.CaptureState(), Is.EqualTo(before));
            Assert.That(service.ScheduledEventCount, Is.EqualTo(1));

            service.Advance(0.00002d);
            Assert.That(scheduledEventFired, Is.True);
        }

        [Test]
        public void MismatchedConfigAndCalendarFields_AreRejectedAtomically()
        {
            var service = new GameTimeService(GameTimeServiceTests.CreateConfig());
            service.Advance(5d);
            GameTimeState before = service.CaptureState();

            GameTimeSaveDto wrongConfig = service.CaptureDto();
            wrongConfig.configId = "time.other.v1";
            Assert.That(service.TryRestoreDto(wrongConfig, out _), Is.False);
            Assert.That(service.CaptureState(), Is.EqualTo(before));

            GameTimeSaveDto wrongDate = service.CaptureDto();
            wrongDate.day++;
            Assert.That(service.TryRestoreDto(wrongDate, out _), Is.False);
            Assert.That(service.CaptureState(), Is.EqualTo(before));

            GameTimeSaveDto invalidRemainder = service.CaptureDto();
            invalidRemainder.fractionalGameTickRemainder = double.NaN;
            Assert.That(service.TryRestoreDto(invalidRemainder, out _), Is.False);
            Assert.That(service.CaptureState(), Is.EqualTo(before));
        }

        [Test]
        public void ValidRestore_ClearsNonSerializableCallbackQueueButKeepsSubscriptions()
        {
            var service = new GameTimeService(GameTimeServiceTests.CreateConfig());
            GameTimeSaveDto dto = service.CaptureDto();
            service.ScheduleAtGameTick(100, "discard-on-restore", Ignore);
            int restoredNotifications = 0;
            using (service.Subscribe((in GameTimeEvent gameEvent) =>
                   {
                       if (gameEvent.Kind == GameTimeEventKind.StateRestored)
                       {
                           restoredNotifications++;
                       }
                   }))
            {
                Assert.That(service.TryRestoreDto(dto, out string failure), Is.True, failure);
            }

            Assert.That(service.ScheduledEventCount, Is.Zero);
            Assert.That(restoredNotifications, Is.EqualTo(1));
        }

        private static void Ignore(in GameTimeEvent _)
        {
        }
    }
}
