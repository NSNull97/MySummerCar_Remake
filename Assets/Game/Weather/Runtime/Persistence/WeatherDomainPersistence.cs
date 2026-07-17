using System;
using System.Collections.Generic;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Wetness;

namespace MSC.Weather.Persistence
{
    public static class WeatherDomainPersistence
    {
        public static WeatherDomainSaveDto Capture(
            WeatherDirector weather,
            GlobalWetnessController wetness,
            LightningStrikeDirector lightning)
        {
            if (weather == null)
            {
                throw new ArgumentNullException(nameof(weather));
            }

            if (wetness == null)
            {
                throw new ArgumentNullException(nameof(wetness));
            }

            if (lightning == null)
            {
                throw new ArgumentNullException(nameof(lightning));
            }

            return new WeatherDomainSaveDto
            {
                Weather = CaptureWeather(weather.CaptureSnapshot()),
                Wetness = CaptureWetness(wetness.CaptureSnapshot()),
                Lightning = CaptureLightning(lightning.CaptureSnapshot()),
            };
        }

        public static void Validate(
            WeatherDomainSaveDto dto,
            WeatherDirector weather,
            GlobalWetnessController wetness,
            LightningStrikeDirector lightning)
        {
            DecodeAndValidate(dto, weather, wetness, lightning, out _, out _, out _);
        }

        public static void RestoreAtomic(
            WeatherDomainSaveDto dto,
            WeatherDirector weather,
            GlobalWetnessController wetness,
            LightningStrikeDirector lightning)
        {
            DecodeAndValidate(
                dto,
                weather,
                wetness,
                lightning,
                out WeatherSnapshot decodedWeather,
                out WetnessSnapshot decodedWetness,
                out LightningDirectorSnapshot decodedLightning);

            WeatherSnapshot previousWeather = weather.CaptureSnapshot();
            WetnessSnapshot previousWetness = wetness.CaptureSnapshot();
            LightningDirectorSnapshot previousLightning = lightning.CaptureSnapshot();
            try
            {
                weather.Restore(decodedWeather);
                wetness.Restore(decodedWetness);
                lightning.Restore(decodedLightning);
            }
            catch
            {
                weather.Restore(previousWeather);
                wetness.Restore(previousWetness);
                lightning.Restore(previousLightning);
                throw;
            }
        }

        private static WeatherSaveDto CaptureWeather(in WeatherSnapshot snapshot)
        {
            WeatherScheduleSnapshot schedule = snapshot.Schedule;
            var dto = new WeatherSaveDto
            {
                ConfigId = schedule.ConfigId,
                SimulationSeconds = snapshot.SimulationSeconds,
                IsScheduleFrozen = snapshot.IsScheduleFrozen,
                CurrentProfileId = schedule.CurrentProfileId.Value,
                TargetProfileId = schedule.TargetProfileId.Value,
                FrontDurationSeconds = schedule.FrontDurationSeconds,
                TransitionDurationSeconds = schedule.TransitionDurationSeconds,
                ElapsedSeconds = schedule.ElapsedSeconds,
                TimelineCursor = schedule.Cursor,
                RandomVersion = schedule.RandomState.Version,
                RandomStateBits = unchecked((long)schedule.RandomState.State),
                RandomIncrementBits = unchecked((long)schedule.RandomState.Increment),
                NextOverrideSequenceBits = unchecked((long)snapshot.NextOverrideSequence),
                Revision = snapshot.Revision,
            };

            for (int index = 0; index < snapshot.Overrides.Length; index++)
            {
                WeatherOverride weatherOverride = snapshot.Overrides[index];
                if (weatherOverride.SerializationPolicy != WeatherOverrideSerializationPolicy.Save)
                {
                    continue;
                }

                dto.Overrides.Add(new WeatherOverrideSaveDto
                {
                    OverrideId = weatherOverride.OverrideId,
                    Owner = weatherOverride.Owner,
                    Reason = weatherOverride.Reason,
                    Priority = weatherOverride.Priority,
                    StartSimulationSeconds = weatherOverride.StartSimulationSeconds,
                    EndSimulationSeconds = weatherOverride.EndSimulationSeconds,
                    RequestedProfileId = weatherOverride.RequestedProfileId.Value,
                    SerializationPolicy = (int)weatherOverride.SerializationPolicy,
                    SequenceBits = unchecked((long)weatherOverride.Sequence),
                });
            }

            return dto;
        }

        private static WetnessSaveDto CaptureWetness(in WetnessSnapshot snapshot) => new WetnessSaveDto
        {
            ConfigId = snapshot.ConfigId,
            GroundWetness01 = snapshot.State.GroundWetness01,
            RoadWetness01 = snapshot.State.RoadWetness01,
            PuddleAmount01 = snapshot.State.PuddleAmount01,
            VegetationWetness01 = snapshot.State.VegetationWetness01,
            Revision = snapshot.Revision,
        };

        private static LightningSaveDto CaptureLightning(in LightningDirectorSnapshot snapshot)
        {
            var dto = new LightningSaveDto
            {
                ConfigId = snapshot.ConfigId,
                SimulationSeconds = snapshot.SimulationSeconds,
                GlobalCooldownUntilSeconds = snapshot.GlobalCooldownUntilSeconds,
                RestoreGraceUntilSeconds = snapshot.RestoreGraceUntilSeconds,
                Sequence = snapshot.Sequence,
                NonLethalMode = snapshot.NonLethalMode,
                RandomVersion = snapshot.RandomState.Version,
                RandomStateBits = unchecked((long)snapshot.RandomState.State),
                RandomIncrementBits = unchecked((long)snapshot.RandomState.Increment),
            };

            for (int index = 0; index < snapshot.CandidateStates.Length; index++)
            {
                LightningCandidateFairnessState state = snapshot.CandidateStates[index];
                dto.CandidateStates.Add(new LightningCandidateSaveDto
                {
                    StableId = state.StableId,
                    CooldownUntilSeconds = state.CooldownUntilSeconds,
                    LastStrikeSequence = state.LastStrikeSequence,
                    StrikeCount = state.StrikeCount,
                });
            }

            return dto;
        }

        private static void DecodeAndValidate(
            WeatherDomainSaveDto dto,
            WeatherDirector weather,
            GlobalWetnessController wetness,
            LightningStrikeDirector lightning,
            out WeatherSnapshot weatherSnapshot,
            out WetnessSnapshot wetnessSnapshot,
            out LightningDirectorSnapshot lightningSnapshot)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (weather == null || wetness == null || lightning == null)
            {
                throw new ArgumentNullException("Weather domain services must not be null.");
            }

            if (dto.SchemaVersion != WeatherDomainSaveDto.CurrentSchemaVersion ||
                !string.Equals(dto.ConfigId, WeatherDomainSaveDto.StableConfigId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported weather domain save schema.", nameof(dto));
            }

            weatherSnapshot = DecodeWeather(dto.Weather);
            wetnessSnapshot = DecodeWetness(dto.Wetness);
            lightningSnapshot = DecodeLightning(dto.Lightning);
            weather.ValidateSnapshot(weatherSnapshot);
            wetness.ValidateSnapshot(wetnessSnapshot);
            lightning.ValidateSnapshot(lightningSnapshot);
        }

        private static WeatherSnapshot DecodeWeather(WeatherSaveDto dto)
        {
            if (dto == null || dto.SchemaVersion != WeatherSaveDto.CurrentSchemaVersion)
            {
                throw new ArgumentException("Unsupported or missing weather DTO.", nameof(dto));
            }

            var randomState = new WeatherRandomState(
                dto.RandomVersion,
                unchecked((ulong)dto.RandomStateBits),
                unchecked((ulong)dto.RandomIncrementBits));
            var schedule = new WeatherScheduleSnapshot(
                dto.ConfigId,
                new WeatherStateId(dto.CurrentProfileId),
                new WeatherStateId(dto.TargetProfileId),
                dto.FrontDurationSeconds,
                dto.TransitionDurationSeconds,
                dto.ElapsedSeconds,
                dto.TimelineCursor,
                randomState);
            List<WeatherOverrideSaveDto> sourceOverrides = dto.Overrides ?? new List<WeatherOverrideSaveDto>();
            var overrides = new WeatherOverride[sourceOverrides.Count];
            for (int index = 0; index < sourceOverrides.Count; index++)
            {
                WeatherOverrideSaveDto value = sourceOverrides[index] ??
                    throw new ArgumentException("Weather override DTO must not be null.", nameof(dto));
                if (value.SerializationPolicy != (int)WeatherOverrideSerializationPolicy.Save)
                {
                    throw new ArgumentException(
                        "Persisted weather override DTO must use the Save serialization policy.",
                        nameof(dto));
                }

                overrides[index] = new WeatherOverride(
                    value.OverrideId,
                    value.Owner,
                    value.Reason,
                    value.Priority,
                    value.StartSimulationSeconds,
                    value.EndSimulationSeconds,
                    new WeatherStateId(value.RequestedProfileId),
                    (WeatherOverrideSerializationPolicy)value.SerializationPolicy,
                    unchecked((ulong)value.SequenceBits));
            }

            return new WeatherSnapshot(
                dto.SimulationSeconds,
                dto.IsScheduleFrozen,
                schedule,
                overrides,
                unchecked((ulong)dto.NextOverrideSequenceBits),
                dto.Revision);
        }

        private static WetnessSnapshot DecodeWetness(WetnessSaveDto dto)
        {
            if (dto == null || dto.SchemaVersion != WetnessSaveDto.CurrentSchemaVersion)
            {
                throw new ArgumentException("Unsupported or missing wetness DTO.", nameof(dto));
            }

            return new WetnessSnapshot(
                dto.ConfigId,
                new WetnessState(
                    dto.GroundWetness01,
                    dto.RoadWetness01,
                    dto.PuddleAmount01,
                    dto.VegetationWetness01),
                dto.Revision);
        }

        private static LightningDirectorSnapshot DecodeLightning(LightningSaveDto dto)
        {
            if (dto == null || dto.SchemaVersion != LightningSaveDto.CurrentSchemaVersion)
            {
                throw new ArgumentException("Unsupported or missing lightning DTO.", nameof(dto));
            }

            List<LightningCandidateSaveDto> sourceStates = dto.CandidateStates ?? new List<LightningCandidateSaveDto>();
            var states = new LightningCandidateFairnessState[sourceStates.Count];
            for (int index = 0; index < sourceStates.Count; index++)
            {
                LightningCandidateSaveDto value = sourceStates[index] ??
                    throw new ArgumentException("Lightning candidate DTO must not be null.", nameof(dto));
                states[index] = new LightningCandidateFairnessState(
                    value.StableId,
                    value.CooldownUntilSeconds,
                    value.LastStrikeSequence,
                    value.StrikeCount);
            }

            return new LightningDirectorSnapshot(
                dto.ConfigId,
                dto.SimulationSeconds,
                dto.GlobalCooldownUntilSeconds,
                dto.RestoreGraceUntilSeconds,
                dto.Sequence,
                dto.NonLethalMode,
                new WeatherRandomState(
                    dto.RandomVersion,
                    unchecked((ulong)dto.RandomStateBits),
                    unchecked((ulong)dto.RandomIncrementBits)),
                states);
        }
    }
}
