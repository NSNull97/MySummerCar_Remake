using System;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Persistence;
using MSC.Weather.Presentation;
using MSC.Weather.Wetness;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Validates the complete environment payload before mutation and restores
    /// weather first, then time. A time restore is independently transactional;
    /// a failure rolls weather back without losing scheduled time callbacks.
    /// </summary>
    public static class ProductionEnvironmentPersistence
    {
        public static ProductionEnvironmentSaveDto Capture(
            GameTimeService gameTime,
            WeatherDirector weather,
            GlobalWetnessController wetness,
            LightningStrikeDirector lightning,
            EnvironmentQualityTier qualityTier)
        {
            RequireServices(gameTime, weather, wetness, lightning);
            ValidateQuality(qualityTier);
            return new ProductionEnvironmentSaveDto
            {
                GameTime = gameTime.CaptureDto(),
                WeatherDomain = WeatherDomainPersistence.Capture(
                    weather,
                    wetness,
                    lightning),
                QualityTier = (int)qualityTier,
            };
        }

        public static void Validate(
            ProductionEnvironmentSaveDto dto,
            GameTimeService gameTime,
            WeatherDirector weather,
            GlobalWetnessController wetness,
            LightningStrikeDirector lightning)
        {
            RequireServices(gameTime, weather, wetness, lightning);
            ValidateEnvelope(dto);
            if (!GameTimeSaveDtoValidator.TryCreateState(
                    dto.GameTime,
                    gameTime.Config,
                    out _,
                    out string timeFailure))
            {
                throw new ArgumentException(
                    "Invalid production game-time state: " + timeFailure,
                    nameof(dto));
            }

            WeatherDomainPersistence.Validate(
                dto.WeatherDomain,
                weather,
                wetness,
                lightning);
        }

        public static EnvironmentQualityTier RestoreAtomic(
            ProductionEnvironmentSaveDto dto,
            GameTimeService gameTime,
            WeatherDirector weather,
            GlobalWetnessController wetness,
            LightningStrikeDirector lightning)
        {
            Validate(dto, gameTime, weather, wetness, lightning);
            EnvironmentQualityTier quality =
                (EnvironmentQualityTier)dto.QualityTier;
            WeatherDomainSaveDto weatherCheckpoint =
                WeatherDomainPersistence.Capture(weather, wetness, lightning);

            WeatherDomainPersistence.RestoreAtomic(
                dto.WeatherDomain,
                weather,
                wetness,
                lightning);
            try
            {
                if (!gameTime.TryRestoreDto(dto.GameTime, out string failure))
                {
                    throw new InvalidOperationException(
                        "Validated game-time restore was rejected: " + failure);
                }
            }
            catch
            {
                WeatherDomainPersistence.RestoreAtomic(
                    weatherCheckpoint,
                    weather,
                    wetness,
                    lightning);
                throw;
            }

            return quality;
        }

        private static void ValidateEnvelope(
            ProductionEnvironmentSaveDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (dto.SchemaVersion !=
                    ProductionEnvironmentSaveDto.CurrentSchemaVersion ||
                !string.Equals(
                    dto.ConfigId,
                    ProductionEnvironmentSaveDto.StableConfigId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Unsupported production environment save schema.",
                    nameof(dto));
            }

            if (dto.GameTime == null || dto.WeatherDomain == null)
            {
                throw new ArgumentException(
                    "Production environment save is incomplete.",
                    nameof(dto));
            }

            ValidateQuality((EnvironmentQualityTier)dto.QualityTier);
        }

        private static void ValidateQuality(
            EnvironmentQualityTier qualityTier)
        {
            if (!Enum.IsDefined(typeof(EnvironmentQualityTier), qualityTier))
            {
                throw new ArgumentOutOfRangeException(nameof(qualityTier));
            }
        }

        private static void RequireServices(
            GameTimeService gameTime,
            WeatherDirector weather,
            GlobalWetnessController wetness,
            LightningStrikeDirector lightning)
        {
            _ = gameTime ?? throw new ArgumentNullException(nameof(gameTime));
            _ = weather ?? throw new ArgumentNullException(nameof(weather));
            _ = wetness ?? throw new ArgumentNullException(nameof(wetness));
            _ = lightning ?? throw new ArgumentNullException(nameof(lightning));
        }
    }
}
