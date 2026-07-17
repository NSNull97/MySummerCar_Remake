using System;

namespace MSC.Core.Time
{
    /// <summary>
    /// Domain-owned versioned DTO. File storage and migrations remain outside the
    /// time domain and belong to the save milestone.
    /// </summary>
    [Serializable]
    public sealed class GameTimeSaveDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string configId = string.Empty;
        public long elapsedGameTicks;
        public double fractionalGameTickRemainder;
        public double timeScale = 1d;
        public bool isPaused;
        public long dayIndex;
        public int year;
        public int month;
        public int day;
        public long timeOfDayTicks;
    }

    public static class GameTimeSaveDtoValidator
    {
        public static bool TryCreateState(
            GameTimeSaveDto dto,
            GameTimeConfig config,
            out GameTimeState state,
            out string failure)
        {
            state = default;
            if (dto == null)
            {
                failure = "Game-time DTO is null.";
                return false;
            }

            if (config == null)
            {
                failure = "Game-time config is null.";
                return false;
            }

            if (dto.schemaVersion != GameTimeSaveDto.CurrentSchemaVersion)
            {
                failure = $"Unsupported game-time schema {dto.schemaVersion}.";
                return false;
            }

            if (!string.Equals(dto.configId, config.ConfigId, StringComparison.Ordinal))
            {
                failure = "Game-time DTO config ID does not match the active config.";
                return false;
            }

            try
            {
                state = new GameTimeState(
                    dto.elapsedGameTicks,
                    dto.fractionalGameTickRemainder,
                    dto.timeScale,
                    dto.isPaused);
            }
            catch (ArgumentOutOfRangeException)
            {
                state = default;
                failure = "Game-time DTO contains an invalid numeric state.";
                return false;
            }

            if (!GameTimeProjection.TryCreateSnapshot(config, state, 0, out GameTimeSnapshot snapshot))
            {
                state = default;
                failure = "Game-time DTO projects outside the supported calendar range.";
                return false;
            }

            if (dto.dayIndex != snapshot.DayIndex ||
                dto.year != snapshot.Date.Year ||
                dto.month != snapshot.Date.Month ||
                dto.day != snapshot.Date.Day ||
                dto.timeOfDayTicks != snapshot.TimeOfDayTicks)
            {
                state = default;
                failure = "Game-time DTO calendar fields do not match its authoritative tick state.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }
}
