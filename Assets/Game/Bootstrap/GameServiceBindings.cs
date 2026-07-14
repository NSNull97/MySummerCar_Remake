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
        {
            GameTime = gameTime ?? throw new ArgumentNullException(nameof(gameTime));
            Weather = weather ?? throw new ArgumentNullException(nameof(weather));
            Save = save ?? throw new ArgumentNullException(nameof(save));
            Audio = audio ?? throw new ArgumentNullException(nameof(audio));
            Interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            EntityIds = entityIds ?? throw new ArgumentNullException(nameof(entityIds));
            WorldStreaming = worldStreaming ?? throw new ArgumentNullException(nameof(worldStreaming));
        }

        public IGameTimeService GameTime { get; }

        public IWeatherService Weather { get; }

        public ISaveService Save { get; }

        public IAudioBackend Audio { get; }

        public IInteractionService Interaction { get; }

        public IEntityIdProvider EntityIds { get; }

        public IWorldStreamingService WorldStreaming { get; }
    }
}
