using System;
using System.IO;
using MSC.Player;
using MSC.Core.Time;
using MSC.Weather.Persistence;
using MSC.Weather.Production;
using UnityEngine;

namespace MSC.Save.Integration
{
    internal static class SaveParticipantJson
    {
        public static string Serialize<T>(T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return JsonUtility.ToJson(value, false);
        }

        public static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException($"Save payload for {typeof(T).Name} is empty.");
            }

            T value;
            try
            {
                value = JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    $"Save payload for {typeof(T).Name} is not valid JSON.",
                    exception);
            }

            return value ?? throw new InvalidDataException(
                $"Save payload for {typeof(T).Name} produced no object.");
        }
    }

    internal sealed class ProductionEnvironmentRestoreBridge
    {
        private readonly ProductionEnvironmentController environment;
        private GameTimeSaveDto preparedTime;
        private GameTimeSaveDto rollbackTime;

        public ProductionEnvironmentRestoreBridge(
            ProductionEnvironmentController environment)
        {
            this.environment = environment ??
                throw new ArgumentNullException(nameof(environment));
        }

        public void PrepareTimeForApply(
            GameTimeSaveDto prepared,
            GameTimeSaveDto checkpoint)
        {
            preparedTime = prepared ?? throw new ArgumentNullException(nameof(prepared));
            rollbackTime = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
        }

        public long GetPreparedAuthoritativeDayIndex()
        {
            if (preparedTime == null)
            {
                throw new InvalidOperationException(
                    "A time-dependent save domain was applied before core.time.");
            }

            return preparedTime.dayIndex;
        }

        public long GetRollbackAuthoritativeDayIndex()
        {
            if (rollbackTime == null)
            {
                throw new InvalidOperationException(
                    "A time-dependent save domain has no core.time rollback checkpoint.");
            }

            return rollbackTime.dayIndex;
        }

        public void StageWeather(WeatherEnvironmentDomainSaveDto weather)
        {
            if (preparedTime == null)
            {
                throw new InvalidOperationException(
                    "Weather restore was applied before its core.time dependency.");
            }

            environment.StageRestore(Compose(preparedTime, weather));
        }

        public void RollbackWeather(WeatherEnvironmentDomainSaveDto weather)
        {
            if (rollbackTime == null)
            {
                throw new InvalidOperationException(
                    "Weather rollback has no core.time checkpoint.");
            }

            environment.StageRestore(Compose(rollbackTime, weather));
        }

        public void Clear()
        {
            preparedTime = null;
            rollbackTime = null;
        }

        private static ProductionEnvironmentSaveDto Compose(
            GameTimeSaveDto time,
            WeatherEnvironmentDomainSaveDto weather)
        {
            if (weather == null)
            {
                throw new ArgumentNullException(nameof(weather));
            }

            return new ProductionEnvironmentSaveDto
            {
                GameTime = time,
                WeatherDomain = weather.weatherDomain,
                QualityTier = weather.qualityTier,
            };
        }
    }

    internal sealed class CoreTimeSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "core.time";

        private readonly ProductionEnvironmentController environment;
        private readonly ProductionEnvironmentRestoreBridge restoreBridge;
        private GameTimeSaveDto checkpoint;

        public CoreTimeSaveParticipant(
            ProductionEnvironmentController environment,
            ProductionEnvironmentRestoreBridge restoreBridge)
        {
            this.environment = environment ??
                throw new ArgumentNullException(nameof(environment));
            this.restoreBridge = restoreBridge ??
                throw new ArgumentNullException(nameof(restoreBridge));
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                GameTimeSaveDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.GlobalState);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload()
        {
            GameTimeSaveDto state =
                environment.AuthoritativeGameTime.CaptureDto();
            // A manual save is currently requested from the pause UI. That UI
            // pause is session presentation state, not a persistent simulation
            // state; restoring it would leave the new session permanently
            // frozen with no UI owner available to resume it.
            NormalizeSessionState(state);
            return SaveParticipantJson.Serialize(state);
        }

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            GameTimeSaveDto state =
                SaveParticipantJson.Deserialize<GameTimeSaveDto>(
                    envelope.PayloadJson);
            // Development saves written before this boundary captured the
            // pause-menu state. Normalize them on read as well so existing
            // slots resume time, weather and needs after loading.
            NormalizeSessionState(state);
            if (!GameTimeSaveDtoValidator.TryCreateState(
                    state,
                    environment.AuthoritativeGameTime.Config,
                    out _,
                    out string failure))
            {
                throw new InvalidDataException(
                    "Core time save preflight failed: " + failure);
            }

            return state;
        }

        internal static void NormalizeSessionState(GameTimeSaveDto state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            state.isPaused = false;
        }

        public object CaptureCheckpoint()
        {
            checkpoint = environment.AuthoritativeGameTime.CaptureDto();
            return checkpoint;
        }

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            restoreBridge.PrepareTimeForApply(
                (GameTimeSaveDto)preparedState,
                checkpoint);
        }

        public void Rollback(object checkpoint)
        {
            restoreBridge.Clear();
            this.checkpoint = null;
        }
    }

    internal sealed class WeatherEnvironmentSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "weather.environment";

        private readonly ProductionEnvironmentController environment;
        private readonly ProductionEnvironmentRestoreBridge restoreBridge;

        public WeatherEnvironmentSaveParticipant(
            ProductionEnvironmentController environment,
            ProductionEnvironmentRestoreBridge restoreBridge)
        {
            this.environment = environment ??
                throw new ArgumentNullException(nameof(environment));
            this.restoreBridge = restoreBridge ??
                throw new ArgumentNullException(nameof(restoreBridge));
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                WeatherEnvironmentDomainSaveDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.Environment,
                CoreTimeSaveParticipant.DomainId);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload()
        {
            ProductionEnvironmentSaveDto current = environment.CaptureState();
            return SaveParticipantJson.Serialize(
                new WeatherEnvironmentDomainSaveDto
                {
                    weatherDomain = current.WeatherDomain,
                    qualityTier = current.QualityTier,
                });
        }

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            WeatherEnvironmentDomainSaveDto state =
                SaveParticipantJson.Deserialize<WeatherEnvironmentDomainSaveDto>(
                    envelope.PayloadJson);
            Validate(state);
            return state;
        }

        public object CaptureCheckpoint()
        {
            ProductionEnvironmentSaveDto current = environment.CaptureState();
            return new WeatherEnvironmentDomainSaveDto
            {
                weatherDomain = current.WeatherDomain,
                qualityTier = current.QualityTier,
            };
        }

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            restoreBridge.StageWeather(
                (WeatherEnvironmentDomainSaveDto)preparedState);
        }

        public void Rollback(object checkpoint)
        {
            restoreBridge.RollbackWeather(
                (WeatherEnvironmentDomainSaveDto)checkpoint);
        }

        private void Validate(WeatherEnvironmentDomainSaveDto state)
        {
            if (state == null)
            {
                throw new InvalidDataException(
                    "Weather environment save preflight failed: payload is missing.");
            }

            if (!state.TryValidateEnvelope(out string failure))
            {
                throw new InvalidDataException(
                    "Weather environment save preflight failed: " + failure);
            }

            try
            {
                WeatherDomainPersistence.Validate(
                    state.weatherDomain,
                    environment.AuthoritativeWeather,
                    environment.AuthoritativeWetness,
                    environment.AuthoritativeLightning);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "Weather environment save preflight failed.",
                    exception);
            }
        }
    }

    internal sealed class PlayerSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "player.state";

        private readonly FirstPersonMotor motor;
        private readonly FirstPersonLook look;

        public PlayerSaveParticipant(GameObject player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            motor = player.GetComponentInChildren<FirstPersonMotor>(true) ??
                throw new InvalidOperationException(
                    "Production player has no FirstPersonMotor save boundary.");
            look = player.GetComponentInChildren<FirstPersonLook>(true) ??
                throw new InvalidOperationException(
                    "Production player has no FirstPersonLook save boundary.");
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                PlayerSaveDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.Player,
                WorldEntitySaveParticipant.DomainId);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(PlayerPersistence.Capture(motor, look));

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            PlayerSaveDto state = SaveParticipantJson.Deserialize<PlayerSaveDto>(
                envelope.PayloadJson);
            if (!state.TryValidate(out string failure) ||
                !motor.CanRestoreSaveState(state.Motor, out failure) ||
                !look.CanRestoreSaveState(state.Look, out failure))
            {
                throw new InvalidDataException(
                    "Player save preflight failed: " + failure);
            }

            return state;
        }

        public object CaptureCheckpoint() =>
            PlayerPersistence.Capture(motor, look);

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            if (!PlayerPersistence.TryRestore(
                    (PlayerSaveDto)preparedState,
                    motor,
                    look,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Player restore failed after preflight: " + failure);
            }
        }

        public void Rollback(object checkpoint)
        {
            if (!PlayerPersistence.TryRestore(
                    (PlayerSaveDto)checkpoint,
                    motor,
                    look,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Player rollback failed: " + failure);
            }
        }
    }
}
