using System;
using MSC.Audio;
using MSC.Core.Identity;
using MSC.Core.Time;
using MSC.Interaction;
using MSC.Save;
using MSC.Weather;
using MSC.World;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Immutable dependency set supplied to the composition root by a concrete bootstrap installer.
    /// </summary>
    public sealed class GameServiceBindings
    {
        public GameServiceBindings(
            IGameTimeService gameTime,
            IWeatherService weather,
            ISaveService save,
            IAudioBackend audio,
            IInteractionService interaction,
            IEntityIdProvider entityIds,
            IWorldStreamingService worldStreaming)
            : this(
                gameTime,
                weather,
                save,
                audio,
                interaction,
                entityIds,
                worldStreaming,
                requireCompleteSet: true)
        {
        }

        private GameServiceBindings(
            IGameTimeService gameTime,
            IWeatherService weather,
            ISaveService save,
            IAudioBackend audio,
            IInteractionService interaction,
            IEntityIdProvider entityIds,
            IWorldStreamingService worldStreaming,
            bool requireCompleteSet)
        {
            GameTime = gameTime;
            Weather = weather;
            Save = save;
            Audio = audio;
            Interaction = interaction;
            EntityIds = entityIds;
            WorldStreaming = worldStreaming;

            if (requireCompleteSet)
            {
                ValidateCompleteSet();
            }
            else if (ServiceCount == 0)
            {
                throw new ArgumentException(
                    "A partial service binding must contain at least one concrete service.");
            }
        }

        /// <summary>
        /// Creates an explicit milestone-scoped binding without fake implementations for unfinished services.
        /// Consumers must still receive their narrow dependency directly from the concrete installer.
        /// </summary>
        public static GameServiceBindings CreatePartial(IInteractionService interaction)
        {
            return new GameServiceBindings(
                gameTime: null,
                weather: null,
                save: null,
                audio: null,
                interaction: interaction ?? throw new ArgumentNullException(nameof(interaction)),
                entityIds: null,
                worldStreaming: null,
                requireCompleteSet: false);
        }

        /// <summary>
        /// Creates the bounded Milestone 05B.1 binding for the production world-streaming service.
        /// </summary>
        public static GameServiceBindings CreateWorldStreamingPartial(IWorldStreamingService worldStreaming)
        {
            return new GameServiceBindings(
                gameTime: null,
                weather: null,
                save: null,
                audio: null,
                interaction: null,
                entityIds: null,
                worldStreaming: worldStreaming ?? throw new ArgumentNullException(nameof(worldStreaming)),
                requireCompleteSet: false);
        }

        /// <summary>
        /// Creates the bounded production environment/world binding without
        /// manufacturing placeholder save, audio, interaction, or identity services.
        /// </summary>
        public static GameServiceBindings CreateProductionEnvironmentPartial(
            IGameTimeService gameTime,
            IWeatherService weather,
            IWorldStreamingService worldStreaming)
        {
            return new GameServiceBindings(
                gameTime: gameTime ?? throw new ArgumentNullException(nameof(gameTime)),
                weather: weather ?? throw new ArgumentNullException(nameof(weather)),
                save: null,
                audio: null,
                interaction: null,
                entityIds: null,
                worldStreaming: worldStreaming ??
                    throw new ArgumentNullException(nameof(worldStreaming)),
                requireCompleteSet: false);
        }

        public IGameTimeService GameTime { get; }

        public IWeatherService Weather { get; }

        public ISaveService Save { get; }

        public IAudioBackend Audio { get; }

        public IInteractionService Interaction { get; }

        public IEntityIdProvider EntityIds { get; }

        public IWorldStreamingService WorldStreaming { get; }

        public int ServiceCount =>
            (GameTime != null ? 1 : 0) +
            (Weather != null ? 1 : 0) +
            (Save != null ? 1 : 0) +
            (Audio != null ? 1 : 0) +
            (Interaction != null ? 1 : 0) +
            (EntityIds != null ? 1 : 0) +
            (WorldStreaming != null ? 1 : 0);

        public bool IsComplete => ServiceCount == 7;

        private void ValidateCompleteSet()
        {
            _ = GameTime ?? throw new ArgumentNullException(nameof(GameTime));
            _ = Weather ?? throw new ArgumentNullException(nameof(Weather));
            _ = Save ?? throw new ArgumentNullException(nameof(Save));
            _ = Audio ?? throw new ArgumentNullException(nameof(Audio));
            _ = Interaction ?? throw new ArgumentNullException(nameof(Interaction));
            _ = EntityIds ?? throw new ArgumentNullException(nameof(EntityIds));
            _ = WorldStreaming ?? throw new ArgumentNullException(nameof(WorldStreaming));
        }
    }
}
