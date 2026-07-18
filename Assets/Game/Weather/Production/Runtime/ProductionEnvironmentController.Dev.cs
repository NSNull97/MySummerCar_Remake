#if UNITY_EDITOR
using System;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Presentation;
using MSC.Weather.Wetness;
using UnityEngine;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Explicit Editor-only controls for production validation. These methods
    /// mutate only project-owned domains; Enviro remains behind the presentation
    /// adapter and none of this API is compiled into player builds.
    /// </summary>
    public sealed partial class ProductionEnvironmentController
    {
        private const string DevManualWeatherOverrideId =
            "production.dev.manual_weather";
        private const int DevManualWeatherOverridePriority = 100000;
        private const double DevManualWeatherOverrideLifetimeSeconds =
            30d * 86400d;
        private const double MaximumDevAdvanceGameSeconds = 30d * 86400d;

        public ulong DevWeatherSeed => initialWeatherSeed;

        public bool DevIsScheduleFrozen
        {
            get
            {
                EnsurePrimaryAndInitialized();
                return weather.IsScheduleFrozen;
            }
        }

        public WeatherTimeline DevTimeline
        {
            get
            {
                EnsurePrimaryAndInitialized();
                return weather.Timeline;
            }
        }

        public bool DevManualWeatherOverrideActive
        {
            get
            {
                EnsurePrimaryAndInitialized();
                return weather.TryGetActiveOverride(
                           out WeatherOverride weatherOverride) &&
                       string.Equals(
                           weatherOverride.OverrideId,
                           DevManualWeatherOverrideId,
                           StringComparison.Ordinal);
            }
        }

        public bool DevTryApplyWeatherOverride(
            string stableStateId,
            float transitionDurationSeconds,
            out string failure)
        {
            EnsurePrimaryAndInitialized();
            if (!WeatherStateId.TryCreate(
                    stableStateId,
                    out WeatherStateId stateId) ||
                !weatherCatalog.Contains(stateId))
            {
                failure = "Unknown logical weather state ID.";
                return false;
            }

            if (!float.IsFinite(transitionDurationSeconds) ||
                transitionDurationSeconds < 0f)
            {
                failure =
                    "Transition duration must be finite and non-negative.";
                return false;
            }

            weather.RemoveOverride(DevManualWeatherOverrideId);
            double now = weather.SimulationSeconds;
            weather.AddOverride(new WeatherOverride(
                DevManualWeatherOverrideId,
                "Production Weather DEV",
                "Explicit production validation state selected in the Editor.",
                DevManualWeatherOverridePriority,
                now,
                now + DevManualWeatherOverrideLifetimeSeconds,
                stateId,
                WeatherOverrideSerializationPolicy.Transient));
            pendingTransitionDurationSeconds = transitionDurationSeconds;
            QueueRefresh(
                EnvironmentRefreshTarget.Sky |
                EnvironmentRefreshTarget.Ambient);
            DevPresentIfReady();
            failure = string.Empty;
            return true;
        }

        public bool DevRemoveWeatherOverride()
        {
            EnsurePrimaryAndInitialized();
            bool removed = weather.RemoveOverride(
                DevManualWeatherOverrideId);
            if (!removed)
            {
                return false;
            }

            pendingTransitionDurationSeconds =
                PresentationIntervalSeconds;
            QueueRefresh(
                EnvironmentRefreshTarget.Sky |
                EnvironmentRefreshTarget.Ambient);
            DevPresentIfReady();
            return true;
        }

        public void DevSetScheduleFrozen(bool frozen)
        {
            EnsurePrimaryAndInitialized();
            weather.SetScheduleFrozen(frozen);
        }

        public void DevSetPaused(bool paused)
        {
            EnsurePrimaryAndInitialized();
            gameTime.SetPaused(paused);
            DevPresentIfReady();
        }

        public bool DevTrySetTimeScale(
            double timeScale,
            out string failure)
        {
            EnsurePrimaryAndInitialized();
            try
            {
                gameTime.SetTimeScale(timeScale);
                failure = string.Empty;
                return true;
            }
            catch (ArgumentOutOfRangeException exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        public bool DevTrySetDateAndTime(
            GameDate date,
            double secondsOfDay,
            out string failure)
        {
            EnsurePrimaryAndInitialized();
            if (!GameDate.TryCreate(
                    date.Year,
                    date.Month,
                    date.Day,
                    out _))
            {
                failure = "Game date must be a valid Gregorian date.";
                return false;
            }

            if (!double.IsFinite(secondsOfDay) ||
                secondsOfDay < 0d ||
                secondsOfDay >= 86400d)
            {
                failure =
                    "Seconds of day must be finite and in [0, 86400).";
                return false;
            }

            long days = gameTime.Config.StartDate.DaysUntil(date);
            long timeOfDayTicks;
            long elapsedTicks;
            try
            {
                timeOfDayTicks = checked((long)Math.Round(
                    secondsOfDay * GameTimeConfig.TicksPerGameSecond,
                    MidpointRounding.AwayFromZero));
                elapsedTicks = checked(
                    days * GameTimeConfig.TicksPerGameDay +
                    timeOfDayTicks -
                    gameTime.Config.StartTimeOfDayTicks);
            }
            catch (OverflowException)
            {
                failure =
                    "Requested game date is outside the supported range.";
                return false;
            }

            if (elapsedTicks < 0 ||
                elapsedTicks > gameTime.Config.MaximumElapsedGameTicks)
            {
                failure =
                    "Requested date/time is before the configured start or " +
                    "outside the supported range.";
                return false;
            }

            GameTimeSaveDto dto = gameTime.CaptureDto();
            dto.elapsedGameTicks = elapsedTicks;
            dto.fractionalGameTickRemainder = 0d;
            dto.dayIndex =
                (gameTime.Config.StartTimeOfDayTicks + elapsedTicks) /
                GameTimeConfig.TicksPerGameDay;
            dto.year = date.Year;
            dto.month = date.Month;
            dto.day = date.Day;
            dto.timeOfDayTicks = timeOfDayTicks;
            if (!gameTime.TryRestoreDto(dto, out failure))
            {
                return false;
            }

            QueueRefresh(
                EnvironmentRefreshTarget.Sky |
                EnvironmentRefreshTarget.Ambient);
            DevPresentIfReady();
            return true;
        }

        public bool DevTryAdvanceGameSeconds(
            double gameSeconds,
            out string failure)
        {
            EnsurePrimaryAndInitialized();
            if (!double.IsFinite(gameSeconds) || gameSeconds <= 0d)
            {
                failure =
                    "Advance amount must be finite and positive.";
                return false;
            }

            if (gameSeconds > MaximumDevAdvanceGameSeconds)
            {
                failure =
                    "A single production DEV advance is limited to 30 game days.";
                return false;
            }

            double gameSecondsPerSimulationSecond =
                86400d /
                gameTime.Config.DayLengthSimulationSeconds *
                gameTime.Snapshot.TimeScale;
            double simulationDelta =
                gameSeconds / gameSecondsPerSimulationSecond;
            if (!DevTryValidateManualAdvance(
                    gameSeconds,
                    simulationDelta,
                    out failure))
            {
                return false;
            }

            try
            {
                gameTime.AdvanceWhileRetainingPause(simulationDelta);
            }
            catch (GameTimeNotificationException exception)
            {
                failure = exception.Message;
                return false;
            }

            weather.Advance(gameSeconds);
            lightning.Advance(gameSeconds);
            AdvanceWetness(gameSeconds);
            DevPresentIfReady();
            failure = string.Empty;
            return true;
        }

        public bool DevTrySetWetness(
            float groundWetness01,
            float roadWetness01,
            float puddleAmount01,
            float vegetationWetness01,
            out string failure)
        {
            EnsurePrimaryAndInitialized();
            try
            {
                wetness.SetState(new WetnessState(
                    groundWetness01,
                    roadWetness01,
                    puddleAmount01,
                    vegetationWetness01));
            }
            catch (ArgumentOutOfRangeException exception)
            {
                failure = exception.Message;
                return false;
            }

            ApplyWetnessOutputs();
            DevPresentIfReady();
            failure = string.Empty;
            return true;
        }

        public bool DevTryResetWeatherSeed(
            ulong seed,
            out string failure)
        {
            EnsurePrimaryAndInitialized();
            WeatherStateId visibleState = weather.CurrentState.Id;
            bool scheduleFrozen = weather.IsScheduleFrozen;
            weather = new WeatherDirector(
                weatherCatalog,
                new WeatherSeed(seed, 7UL),
                WeatherStateIds.Clear);
            weather.SetScheduleFrozen(scheduleFrozen);
            initialWeatherSeed = seed;
            return DevTryApplyWeatherOverride(
                visibleState.Value,
                0f,
                out failure);
        }

        public bool DevTryTriggerAmbientLightningAtListener(
            out EnvironmentPresentationStatus status,
            out string failure)
        {
            EnsurePrimaryAndInitialized();
            if (listener == null)
            {
                status = PresentationStatus;
                failure =
                    "Production presentation listener/camera is not bound.";
                return false;
            }

            status = TriggerAmbientLightning(listener.position, 1f);
            failure = status.State == EnvironmentPresentationState.Faulted
                ? BuildAdapterFailure(
                    "Ambient lightning presentation failed.")
                : string.Empty;
            return string.IsNullOrEmpty(failure);
        }

        public EnvironmentPresentationStatus DevRefreshPresentation()
        {
            EnsurePrimaryAndInitialized();
            return IsWorldRevealReady
                ? PresentCurrentState()
                : PresentationStatus;
        }

        public bool DevTryGetListenerWorldPosition(
            out Vector3 worldPosition)
        {
            EnsurePrimaryAndInitialized();
            if (listener == null)
            {
                worldPosition = default;
                return false;
            }

            worldPosition = listener.position;
            return true;
        }

        private bool DevTryValidateManualAdvance(
            double gameSeconds,
            double simulationDelta,
            out string failure)
        {
            try
            {
                var timeProbe = new GameTimeService(gameTime.Config);
                if (!timeProbe.TryRestoreDto(
                        gameTime.CaptureDto(),
                        out failure))
                {
                    return false;
                }

                timeProbe.AdvanceWhileRetainingPause(simulationDelta);
                var weatherProbe = new WeatherDirector(
                    weatherCatalog,
                    new WeatherSeed(initialWeatherSeed, 7UL),
                    WeatherStateIds.Clear);
                weatherProbe.Restore(weather.CaptureSnapshot());
                weatherProbe.Advance(gameSeconds);
                if (!double.IsFinite(
                        lightning.SimulationSeconds + gameSeconds))
                {
                    failure =
                        "Lightning simulation time would overflow.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is OverflowException)
            {
                failure = exception.Message;
                return false;
            }
        }

        private void DevPresentIfReady()
        {
            if (IsWorldRevealReady)
            {
                PresentCurrentState();
            }
        }
    }
}
#endif
