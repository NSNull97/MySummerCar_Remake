using MSC.Audio;
using MSC.Audio.PlayerIntegration;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.AudioPlayerIntegration
{
    public sealed class PlayerVoiceReactionPolicyTests
    {
        [Test]
        public void ManualSwear_UsesDonorSpeechLockAndStressRelief()
        {
            var policy = new PlayerVoiceReactionPolicy(123u);

            Assert.That(
                policy.TryRequest(
                    PlayerVoiceReactionKind.ManualSwear,
                    0d,
                    42f,
                    out PlayerVoiceReactionDecision first),
                Is.True);
            Assert.That(
                first.StressRelief,
                Is.EqualTo(PlayerVoiceReactionPolicy.SwearStressRelief));
            Assert.That(first.VariantIndex, Is.InRange(0, 15));
            Assert.That(
                policy.TryRequest(
                    PlayerVoiceReactionKind.MiddleFinger,
                    0.999d,
                    42f,
                    out _),
                Is.False);
            Assert.That(
                policy.TryRequest(
                    PlayerVoiceReactionKind.MiddleFinger,
                    1d,
                    42f,
                    out PlayerVoiceReactionDecision second),
                Is.True);
            Assert.That(second.StressRelief, Is.Zero);
            Assert.That(second.VariantIndex, Is.InRange(0, 10));
        }

        [Test]
        public void HighStress_ReactsAtMaximumOnceUntilStressDrops()
        {
            var policy = new PlayerVoiceReactionPolicy(456u);

            Assert.That(
                policy.TryRequest(
                    PlayerVoiceReactionKind.AutomaticHighStress,
                    0d,
                    99.999f,
                    out _),
                Is.False);
            Assert.That(
                policy.TryRequest(
                    PlayerVoiceReactionKind.AutomaticHighStress,
                    0d,
                    100f,
                    out PlayerVoiceReactionDecision first),
                Is.True);
            Assert.That(first.StressRelief, Is.EqualTo(0.5f));
            Assert.That(
                policy.TryRequest(
                    PlayerVoiceReactionKind.AutomaticHighStress,
                    2d,
                    100f,
                    out _),
                Is.False);

            policy.ObserveStress(99.5f);
            Assert.That(
                policy.TryRequest(
                    PlayerVoiceReactionKind.AutomaticHighStress,
                    2d,
                    100f,
                    out _),
                Is.True);
        }

        [Test]
        public void EventOnlyReactions_DoNotMutateStress()
        {
            var policy = new PlayerVoiceReactionPolicy(789u);

            Assert.That(
                policy.TryRequest(
                    PlayerVoiceReactionKind.InsufficientFunds,
                    0d,
                    88f,
                    out PlayerVoiceReactionDecision money),
                Is.True);
            Assert.That(money.StressRelief, Is.Zero);
            Assert.That(
                policy.TryRequest(
                    PlayerVoiceReactionKind.MiddleFinger,
                    1d,
                    88f,
                    out PlayerVoiceReactionDecision finger),
                Is.True);
            Assert.That(finger.StressRelief, Is.Zero);
        }

        [Test]
        public void Catalog_ExposesAllStableVoiceEventsAndLocalizedSubtitles()
        {
            Assert.That(AudioProjectIds.Events.PlayerSwearVariantCount, Is.EqualTo(16));
            for (int index = 0;
                 index < AudioProjectIds.Events.PlayerSwearVariantCount;
                 index++)
            {
                Assert.That(
                    AudioProjectIds.Events.GetPlayerSwearVariant(index).Value,
                    Is.EqualTo($"audio.event.player.swear.{index + 1:00}"));
                Assert.That(
                    PlayerVoiceReactionCatalog.GetSubtitle(
                        PlayerVoiceReactionKind.ManualSwear,
                        index,
                        SystemLanguage.English),
                    Is.Not.Empty);
                Assert.That(
                    PlayerVoiceReactionCatalog.GetSubtitle(
                        PlayerVoiceReactionKind.ManualSwear,
                        index,
                        SystemLanguage.Russian),
                    Is.Not.Empty);
            }

            Assert.That(
                AudioProjectIds.Events.PlayerMiddleFingerVariantCount,
                Is.EqualTo(11));
            string[] expectedMiddleFingerSubtitles =
            {
                "Smell pussy!",
                "Go to pussy, satan.",
                "Smell a shit!",
                "Go to pussy, please.",
                "You dork of the pussy.",
                "You, go home.",
                "Go to hell.",
                "I don't have energy to watch your face.",
                "You dick of a pussy.",
                "You dick of a pussy!",
                "You truly are a wart of a dick.",
            };
            for (int index = 0;
                 index < AudioProjectIds.Events.PlayerMiddleFingerVariantCount;
                 index++)
            {
                Assert.That(
                    AudioProjectIds.Events
                        .GetPlayerMiddleFingerVariant(index).Value,
                    Is.EqualTo($"audio.event.player.finger.{index + 1:00}"));
                Assert.That(
                    PlayerVoiceReactionCatalog.GetSubtitle(
                        PlayerVoiceReactionKind.MiddleFinger,
                        index,
                        SystemLanguage.English),
                    Is.EqualTo(expectedMiddleFingerSubtitles[index]));
                Assert.That(
                    PlayerVoiceReactionCatalog.GetSubtitle(
                        PlayerVoiceReactionKind.MiddleFinger,
                        index,
                        SystemLanguage.Russian),
                    Is.Not.Empty);
            }
        }

        [Test]
        public void MiddleFinger_UsesItsSeparateElevenLineDonorCatalog()
        {
            var policy = new PlayerVoiceReactionPolicy(321u);

            for (int attempt = 0; attempt < 32; attempt++)
            {
                Assert.That(
                    policy.TryRequest(
                        PlayerVoiceReactionKind.MiddleFinger,
                        attempt,
                        20f,
                        out PlayerVoiceReactionDecision decision),
                    Is.True);
                Assert.That(decision.VariantIndex, Is.InRange(0, 10));
            }
        }
    }
}
