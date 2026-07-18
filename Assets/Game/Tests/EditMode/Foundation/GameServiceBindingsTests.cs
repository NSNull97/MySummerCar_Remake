using System;
using MSC.Bootstrap;
using MSC.Core.Time;
using MSC.Interaction;
using MSC.Weather.Domain;
using MSC.World;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.Foundation
{
    public sealed class GameServiceBindingsTests
    {
        [Test]
        public void PartialBindingsInitializeRootWithoutFakeFutureServices()
        {
            var interaction = new InteractionServiceStub();
            GameServiceBindings bindings = GameServiceBindings.CreatePartial(interaction);
            var rootObject = new GameObject("PartialCompositionRoot");

            try
            {
                GameCompositionRoot root = rootObject.AddComponent<GameCompositionRoot>();
                root.Initialize(bindings);

                Assert.That(bindings.Interaction, Is.SameAs(interaction));
                Assert.That(bindings.ServiceCount, Is.EqualTo(1));
                Assert.That(bindings.IsComplete, Is.False);
                Assert.That(root.IsInitialized, Is.True);
                Assert.That(root.BoundServiceCount, Is.EqualTo(1));
                Assert.That(root.HasCompleteBindings, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void NullPartialInteractionBindingIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => GameServiceBindings.CreatePartial(null));
        }

        [Test]
        public void WorldStreamingPartialBindingContainsOnlyConcreteStreamingService()
        {
            var worldStreaming = new WorldStreamingServiceStub();

            GameServiceBindings bindings = GameServiceBindings.CreateWorldStreamingPartial(worldStreaming);

            Assert.That(bindings.WorldStreaming, Is.SameAs(worldStreaming));
            Assert.That(bindings.ServiceCount, Is.EqualTo(1));
            Assert.That(bindings.IsComplete, Is.False);
        }

        [Test]
        public void NullWorldStreamingPartialBindingIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => GameServiceBindings.CreateWorldStreamingPartial(null));
        }

        [Test]
        public void ProductionEnvironmentPartialBindsTimeWeatherAndWorldOnly()
        {
            var gameTime = new GameTimeService();
            var weather = new WeatherDirector(
                WeatherProfileCatalog.CreateRemakeDesignTargets(),
                new WeatherSeed(1UL, 7UL),
                WeatherStateIds.Clear);
            var worldStreaming = new WorldStreamingServiceStub();

            GameServiceBindings bindings =
                GameServiceBindings.CreateProductionEnvironmentPartial(
                    gameTime,
                    weather,
                    worldStreaming);

            Assert.That(bindings.GameTime, Is.SameAs(gameTime));
            Assert.That(bindings.Weather, Is.SameAs(weather));
            Assert.That(bindings.WorldStreaming, Is.SameAs(worldStreaming));
            Assert.That(bindings.ServiceCount, Is.EqualTo(3));
            Assert.That(bindings.IsComplete, Is.False);
        }

        private sealed class InteractionServiceStub : IInteractionService
        {
            public bool IsInteractionEnabled => true;
        }

        private sealed class WorldStreamingServiceStub : IWorldStreamingService
        {
            public bool IsStreaming => false;
        }
    }
}
