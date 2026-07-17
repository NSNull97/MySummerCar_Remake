using System;

namespace MSC.Core.Time
{
    /// <summary>
    /// Authoritative project-owned game clock. Callers supply explicit simulation
    /// deltas; the service never reads the wall clock or Unity time implicitly.
    /// </summary>
    public interface IGameTimeService
    {
        /// <summary>
        /// Backward-compatible elapsed game time, in game seconds, from the
        /// configured start instant.
        /// </summary>
        double CurrentGameTimeSeconds { get; }

        GameTimeSnapshot Snapshot { get; }

        GameTimeState CaptureState();

        GameTimeSaveDto CaptureDto();

        void Advance(double simulationDeltaSeconds);

        void SetPaused(bool paused);

        void SetTimeScale(double timeScale);

        IDisposable Subscribe(GameTimeEventHandler handler);

        GameTimeScheduleHandle ScheduleAtGameTick(
            long absoluteGameTick,
            string eventId,
            GameTimeEventHandler handler);

        bool CancelScheduledEvent(GameTimeScheduleHandle handle);

        bool TryRestoreDto(GameTimeSaveDto dto, out string failure);
    }
}
