using System;
using System.Collections.Generic;

namespace MSC.Core.Time
{
    /// <summary>
    /// Deterministic project-owned game clock. It advances only when explicitly
    /// supplied a finite non-negative simulation delta by its composition owner.
    /// </summary>
    public sealed class GameTimeService : IGameTimeService
    {
        private readonly GameTimeConfig config;
        private readonly GameTimeScheduler scheduler = new GameTimeScheduler();
        private readonly List<Subscription> subscriptions = new List<Subscription>();

        private GameTimeState state;
        private GameTimeSnapshot snapshot;
        private ulong revision;
        private int notificationDepth;
        private bool mutationInProgress;
        private bool subscriptionsNeedCompaction;

        public GameTimeService()
            : this(GameTimeConfig.RemakeDesignTargetDefaults)
        {
        }

        public GameTimeService(GameTimeConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            state = new GameTimeState(0, 0d, config.DefaultTimeScale, false);
            if (!GameTimeProjection.TryCreateSnapshot(config, state, revision, out snapshot))
            {
                throw new ArgumentException("The game-time config cannot produce an initial snapshot.");
            }
        }

        public GameTimeConfig Config => config;

        public double CurrentGameTimeSeconds => state.ElapsedGameSeconds;

        public GameTimeSnapshot Snapshot => snapshot;

        public int ScheduledEventCount => scheduler.ScheduledCount;

        public GameTimeState CaptureState()
        {
            return state;
        }

        public GameTimeSaveDto CaptureDto()
        {
            return new GameTimeSaveDto
            {
                schemaVersion = GameTimeSaveDto.CurrentSchemaVersion,
                configId = config.ConfigId,
                elapsedGameTicks = state.ElapsedGameTicks,
                fractionalGameTickRemainder = state.FractionalGameTickRemainder,
                timeScale = state.TimeScale,
                isPaused = state.IsPaused,
                dayIndex = snapshot.DayIndex,
                year = snapshot.Date.Year,
                month = snapshot.Date.Month,
                day = snapshot.Date.Day,
                timeOfDayTicks = snapshot.TimeOfDayTicks
            };
        }

        public void Advance(double simulationDeltaSeconds)
        {
            AdvanceCore(simulationDeltaSeconds, advanceWhilePaused: false);
        }

        /// <summary>
        /// DEV/capture operation that advances the clock while retaining its
        /// current pause state. It is one transactional mutation, so observers
        /// never see a temporary unpause notification.
        /// </summary>
        public void AdvanceWhileRetainingPause(double simulationDeltaSeconds)
        {
            AdvanceCore(simulationDeltaSeconds, advanceWhilePaused: true);
        }

        private void AdvanceCore(
            double simulationDeltaSeconds,
            bool advanceWhilePaused)
        {
            EnterMutation();
            GameTimeState checkpointState = state;
            GameTimeSnapshot checkpointSnapshot = snapshot;
            ulong checkpointRevision = revision;
            GameTimeScheduler.Checkpoint schedulerCheckpoint = null;
            try
            {
                if (!GameTimeConfig.IsFinite(simulationDeltaSeconds) || simulationDeltaSeconds < 0d)
                {
                    throw new ArgumentOutOfRangeException(nameof(simulationDeltaSeconds));
                }

                if (simulationDeltaSeconds == 0d || state.IsPaused && !advanceWhilePaused)
                {
                    return;
                }

                double generatedTicks =
                    simulationDeltaSeconds *
                    config.GameTicksPerSimulationSecondAtScaleOne *
                    state.TimeScale +
                    state.FractionalGameTickRemainder;
                if (!GameTimeConfig.IsFinite(generatedTicks) || generatedTicks < 0d)
                {
                    throw new OverflowException("Game-time advancement exceeded finite numeric range.");
                }

                double maximumAdditionalTicks = config.MaximumElapsedGameTicks - state.ElapsedGameTicks;
                if (generatedTicks > maximumAdditionalTicks + 1d)
                {
                    throw new OverflowException("Game-time advancement exceeded the supported calendar range.");
                }

                long wholeTicks = checked((long)Math.Floor(generatedTicks));
                double remainder = generatedTicks - wholeTicks;
                if (remainder >= 1d)
                {
                    wholeTicks = checked(wholeTicks + 1L);
                    remainder -= 1d;
                }

                long nextElapsedTicks = checked(state.ElapsedGameTicks + wholeTicks);
                var nextState = new GameTimeState(
                    nextElapsedTicks,
                    remainder,
                    state.TimeScale,
                    state.IsPaused);
                GameTimeSnapshot previous = snapshot;
                if (scheduler.HasDue(nextElapsedTicks))
                {
                    // Scheduling is a rare path. Allocate a queue checkpoint only
                    // when this advance can actually consume due callbacks.
                    schedulerCheckpoint = scheduler.CaptureCheckpoint();
                }

                ApplyValidatedState(nextState);

                scheduler.DispatchDue(
                    state.ElapsedGameTicks,
                    previous,
                    snapshot,
                    Publish);

                if (previous.DayIndex != snapshot.DayIndex)
                {
                    var dayChanged = new GameTimeEvent(
                        GameTimeEventKind.DayChanged,
                        previous,
                        snapshot);
                    Publish(in dayChanged);
                }

                var advanced = new GameTimeEvent(GameTimeEventKind.Advanced, previous, snapshot);
                Publish(in advanced);
            }
            catch
            {
                state = checkpointState;
                snapshot = checkpointSnapshot;
                revision = checkpointRevision;
                if (schedulerCheckpoint != null)
                {
                    scheduler.RestoreCheckpoint(schedulerCheckpoint);
                }

                throw;
            }
            finally
            {
                ExitMutation();
            }
        }

        public void SetPaused(bool paused)
        {
            EnterMutation();
            GameTimeState checkpointState = state;
            GameTimeSnapshot checkpointSnapshot = snapshot;
            ulong checkpointRevision = revision;
            try
            {
                if (state.IsPaused == paused)
                {
                    return;
                }

                GameTimeSnapshot previous = snapshot;
                var nextState = new GameTimeState(
                    state.ElapsedGameTicks,
                    state.FractionalGameTickRemainder,
                    state.TimeScale,
                    paused);
                ApplyValidatedState(nextState);
                var changed = new GameTimeEvent(
                    GameTimeEventKind.PauseChanged,
                    previous,
                    snapshot);
                Publish(in changed);
            }
            catch
            {
                state = checkpointState;
                snapshot = checkpointSnapshot;
                revision = checkpointRevision;
                throw;
            }
            finally
            {
                ExitMutation();
            }
        }

        public void SetTimeScale(double timeScale)
        {
            EnterMutation();
            GameTimeState checkpointState = state;
            GameTimeSnapshot checkpointSnapshot = snapshot;
            ulong checkpointRevision = revision;
            try
            {
                if (!GameTimeConfig.IsValidTimeScale(timeScale))
                {
                    throw new ArgumentOutOfRangeException(nameof(timeScale));
                }

                if (state.TimeScale.Equals(timeScale))
                {
                    return;
                }

                GameTimeSnapshot previous = snapshot;
                var nextState = new GameTimeState(
                    state.ElapsedGameTicks,
                    state.FractionalGameTickRemainder,
                    timeScale,
                    state.IsPaused);
                ApplyValidatedState(nextState);
                var changed = new GameTimeEvent(
                    GameTimeEventKind.TimeScaleChanged,
                    previous,
                    snapshot);
                Publish(in changed);
            }
            catch
            {
                state = checkpointState;
                snapshot = checkpointSnapshot;
                revision = checkpointRevision;
                throw;
            }
            finally
            {
                ExitMutation();
            }
        }

        public IDisposable Subscribe(GameTimeEventHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var subscription = new Subscription(this, handler);
            subscriptions.Add(subscription);
            return subscription;
        }

        public GameTimeScheduleHandle ScheduleAtGameTick(
            long absoluteGameTick,
            string eventId,
            GameTimeEventHandler handler)
        {
            EnsureSchedulerMutationAllowed();
            if (absoluteGameTick <= state.ElapsedGameTicks ||
                absoluteGameTick > config.MaximumElapsedGameTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(absoluteGameTick));
            }

            return scheduler.Schedule(absoluteGameTick, eventId, handler);
        }

        public bool CancelScheduledEvent(GameTimeScheduleHandle handle)
        {
            EnsureSchedulerMutationAllowed();
            return scheduler.Cancel(handle);
        }

        public bool TryRestoreDto(GameTimeSaveDto dto, out string failure)
        {
            EnterMutation();
            GameTimeState checkpointState = state;
            GameTimeSnapshot checkpointSnapshot = snapshot;
            ulong checkpointRevision = revision;
            GameTimeScheduler.Checkpoint schedulerCheckpoint = null;
            try
            {
                if (!GameTimeSaveDtoValidator.TryCreateState(
                        dto,
                        config,
                        out GameTimeState restoredState,
                        out failure))
                {
                    return false;
                }

                if (!GameTimeProjection.TryCreateSnapshot(
                        config,
                        restoredState,
                        NextRevision(),
                        out GameTimeSnapshot restoredSnapshot))
                {
                    failure = "Game-time DTO could not produce a valid snapshot.";
                    return false;
                }

                GameTimeSnapshot previous = snapshot;
                if (scheduler.ScheduledCount > 0)
                {
                    schedulerCheckpoint = scheduler.CaptureCheckpoint();
                }

                revision = restoredSnapshot.Revision;
                state = restoredState;
                snapshot = restoredSnapshot;
                scheduler.Clear();

                var restored = new GameTimeEvent(
                    GameTimeEventKind.StateRestored,
                    previous,
                    snapshot);
                Publish(in restored);
                failure = string.Empty;
                return true;
            }
            catch
            {
                state = checkpointState;
                snapshot = checkpointSnapshot;
                revision = checkpointRevision;
                if (schedulerCheckpoint != null)
                {
                    scheduler.RestoreCheckpoint(schedulerCheckpoint);
                }

                throw;
            }
            finally
            {
                ExitMutation();
            }
        }

        private void EnterMutation()
        {
            if (mutationInProgress)
            {
                throw new InvalidOperationException(
                    "Game-time state cannot be mutated reentrantly from a clock notification.");
            }

            mutationInProgress = true;
        }

        private void ExitMutation()
        {
            mutationInProgress = false;
        }

        private void EnsureSchedulerMutationAllowed()
        {
            if (mutationInProgress)
            {
                throw new InvalidOperationException(
                    "Game-time scheduled events cannot be added or cancelled from a clock notification.");
            }
        }

        private void ApplyValidatedState(GameTimeState nextState)
        {
            ulong nextRevision = NextRevision();
            if (!GameTimeProjection.TryCreateSnapshot(
                    config,
                    nextState,
                    nextRevision,
                    out GameTimeSnapshot nextSnapshot))
            {
                throw new OverflowException("Game-time state projects outside the supported calendar range.");
            }

            revision = nextRevision;
            state = nextState;
            snapshot = nextSnapshot;
        }

        private ulong NextRevision()
        {
            if (revision == ulong.MaxValue)
            {
                throw new InvalidOperationException("Game-time revision space is exhausted.");
            }

            return revision + 1UL;
        }

        private void Publish(in GameTimeEvent gameTimeEvent)
        {
            notificationDepth++;
            int countAtStart = subscriptions.Count;
            try
            {
                for (int index = 0; index < countAtStart; index++)
                {
                    Subscription subscription = subscriptions[index];
                    if (subscription.IsActive)
                    {
                        try
                        {
                            subscription.Handler.Invoke(in gameTimeEvent);
                        }
                        catch (GameTimeNotificationException)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            throw new GameTimeNotificationException(
                                gameTimeEvent.Kind,
                                gameTimeEvent.ScheduledEventId,
                                exception);
                        }
                    }
                }
            }
            finally
            {
                notificationDepth--;
                if (notificationDepth == 0 && subscriptionsNeedCompaction)
                {
                    CompactSubscriptions();
                }
            }
        }

        private void RemoveSubscription(Subscription subscription)
        {
            subscription.Deactivate();
            if (notificationDepth > 0)
            {
                subscriptionsNeedCompaction = true;
                return;
            }

            subscriptions.Remove(subscription);
        }

        private void CompactSubscriptions()
        {
            for (int index = subscriptions.Count - 1; index >= 0; index--)
            {
                if (!subscriptions[index].IsActive)
                {
                    subscriptions.RemoveAt(index);
                }
            }

            subscriptionsNeedCompaction = false;
        }

        private sealed class Subscription : IDisposable
        {
            private GameTimeService owner;

            public Subscription(GameTimeService owner, GameTimeEventHandler handler)
            {
                this.owner = owner;
                Handler = handler;
            }

            public GameTimeEventHandler Handler { get; }

            public bool IsActive => owner != null;

            public void Dispose()
            {
                GameTimeService currentOwner = owner;
                if (currentOwner != null)
                {
                    currentOwner.RemoveSubscription(this);
                }
            }

            public void Deactivate()
            {
                owner = null;
            }
        }
    }
}
