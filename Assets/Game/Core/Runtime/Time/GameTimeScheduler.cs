using System;
using System.Collections.Generic;

namespace MSC.Core.Time
{
    public readonly struct GameTimeScheduleHandle : IEquatable<GameTimeScheduleHandle>
    {
        internal GameTimeScheduleHandle(long value)
        {
            Value = value;
        }

        public long Value { get; }

        public bool IsValid => Value > 0;

        public bool Equals(GameTimeScheduleHandle other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is GameTimeScheduleHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(
            GameTimeScheduleHandle left,
            GameTimeScheduleHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            GameTimeScheduleHandle left,
            GameTimeScheduleHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Ordered one-shot event queue. Equal due ticks preserve insertion order.
    /// Scheduling is intentionally rare-path; clock advancement itself remains
    /// allocation-free when no callbacks are due.
    /// </summary>
    public sealed class GameTimeScheduler
    {
        private const int MaximumEventIdLength = 64;

        private readonly List<ScheduledEntry> entries = new List<ScheduledEntry>();
        private long nextHandle = 1;
        private long nextInsertionOrder = 1;

        public int ScheduledCount => entries.Count;

        internal bool HasDue(long throughGameTick)
        {
            return entries.Count > 0 && entries[0].AbsoluteGameTick <= throughGameTick;
        }

        internal Checkpoint CaptureCheckpoint()
        {
            return new Checkpoint(entries.ToArray(), nextHandle, nextInsertionOrder);
        }

        internal void RestoreCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null)
            {
                throw new ArgumentNullException(nameof(checkpoint));
            }

            entries.Clear();
            entries.AddRange(checkpoint.Entries);
            nextHandle = checkpoint.NextHandle;
            nextInsertionOrder = checkpoint.NextInsertionOrder;
        }

        internal GameTimeScheduleHandle Schedule(
            long absoluteGameTick,
            string eventId,
            GameTimeEventHandler handler)
        {
            if (absoluteGameTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(absoluteGameTick));
            }

            if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > MaximumEventIdLength)
            {
                throw new ArgumentException(
                    "Scheduled event ID must contain 1-64 non-whitespace characters.",
                    nameof(eventId));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (nextHandle == long.MaxValue || nextInsertionOrder == long.MaxValue)
            {
                throw new InvalidOperationException("Game-time scheduler sequence space is exhausted.");
            }

            var handle = new GameTimeScheduleHandle(nextHandle++);
            var entry = new ScheduledEntry(
                handle,
                absoluteGameTick,
                nextInsertionOrder++,
                eventId,
                handler);

            int insertionIndex = FindInsertionIndex(entry);
            entries.Insert(insertionIndex, entry);
            return handle;
        }

        public bool Cancel(GameTimeScheduleHandle handle)
        {
            if (!handle.IsValid)
            {
                return false;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].Handle == handle)
                {
                    entries.RemoveAt(index);
                    return true;
                }
            }

            return false;
        }

        internal void DispatchDue(
            long throughGameTick,
            GameTimeSnapshot previous,
            GameTimeSnapshot current,
            GameTimeEventHandler publish)
        {
            while (entries.Count > 0 && entries[0].AbsoluteGameTick <= throughGameTick)
            {
                ScheduledEntry entry = entries[0];
                entries.RemoveAt(0);

                var scheduledEvent = new GameTimeEvent(
                    GameTimeEventKind.Scheduled,
                    previous,
                    current,
                    entry.EventId,
                    entry.AbsoluteGameTick);
                publish?.Invoke(in scheduledEvent);
                try
                {
                    entry.Handler.Invoke(in scheduledEvent);
                }
                catch (GameTimeNotificationException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new GameTimeNotificationException(
                        scheduledEvent.Kind,
                        entry.EventId,
                        exception);
                }
            }
        }

        internal void Clear()
        {
            entries.Clear();
        }

        internal sealed class Checkpoint
        {
            internal Checkpoint(
                ScheduledEntry[] entries,
                long nextHandle,
                long nextInsertionOrder)
            {
                Entries = entries ?? Array.Empty<ScheduledEntry>();
                NextHandle = nextHandle;
                NextInsertionOrder = nextInsertionOrder;
            }

            internal ScheduledEntry[] Entries { get; }

            internal long NextHandle { get; }

            internal long NextInsertionOrder { get; }
        }

        private int FindInsertionIndex(ScheduledEntry candidate)
        {
            int low = 0;
            int high = entries.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                ScheduledEntry existing = entries[middle];
                bool existingComesFirst = existing.AbsoluteGameTick < candidate.AbsoluteGameTick ||
                                          existing.AbsoluteGameTick == candidate.AbsoluteGameTick &&
                                          existing.InsertionOrder < candidate.InsertionOrder;
                if (existingComesFirst)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        internal sealed class ScheduledEntry
        {
            public ScheduledEntry(
                GameTimeScheduleHandle handle,
                long absoluteGameTick,
                long insertionOrder,
                string eventId,
                GameTimeEventHandler handler)
            {
                Handle = handle;
                AbsoluteGameTick = absoluteGameTick;
                InsertionOrder = insertionOrder;
                EventId = eventId;
                Handler = handler;
            }

            public GameTimeScheduleHandle Handle { get; }

            public long AbsoluteGameTick { get; }

            public long InsertionOrder { get; }

            public string EventId { get; }

            public GameTimeEventHandler Handler { get; }
        }
    }
}
