using System;

namespace MSC.Core.Time
{
    public enum GameTimeEventKind
    {
        Advanced = 0,
        DayChanged = 1,
        PauseChanged = 2,
        TimeScaleChanged = 3,
        StateRestored = 4,
        Scheduled = 5
    }

    public delegate void GameTimeEventHandler(in GameTimeEvent gameTimeEvent);

    /// <summary>
    /// Wraps an exception raised by a clock observer or scheduled callback.
    /// The clock restores its own state and scheduled queue before exposing the
    /// failure to the composition owner.
    /// </summary>
    public sealed class GameTimeNotificationException : Exception
    {
        public GameTimeNotificationException(
            GameTimeEventKind eventKind,
            string eventId,
            Exception innerException)
            : base(BuildMessage(eventKind, eventId), innerException)
        {
            EventKind = eventKind;
            EventId = eventId ?? string.Empty;
        }

        public GameTimeEventKind EventKind { get; }

        public string EventId { get; }

        private static string BuildMessage(GameTimeEventKind eventKind, string eventId)
        {
            return string.IsNullOrEmpty(eventId)
                ? $"A game-time observer failed while handling '{eventKind}'."
                : $"Game-time callback '{eventId}' failed while handling '{eventKind}'.";
        }
    }

    /// <summary>
    /// Immutable event payload. Scheduled events additionally expose their stable
    /// event ID and exact due tick.
    /// </summary>
    public readonly struct GameTimeEvent
    {
        internal GameTimeEvent(
            GameTimeEventKind kind,
            GameTimeSnapshot previous,
            GameTimeSnapshot current,
            string scheduledEventId = "",
            long scheduledGameTick = 0)
        {
            Kind = kind;
            Previous = previous;
            Current = current;
            ScheduledEventId = scheduledEventId ?? string.Empty;
            ScheduledGameTick = scheduledGameTick;
        }

        public GameTimeEventKind Kind { get; }

        public GameTimeSnapshot Previous { get; }

        public GameTimeSnapshot Current { get; }

        public string ScheduledEventId { get; }

        public long ScheduledGameTick { get; }

        public bool IsScheduled => Kind == GameTimeEventKind.Scheduled;
    }
}
