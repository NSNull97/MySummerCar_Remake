using System;
using System.Collections.Generic;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WeatherDomain
{
    public sealed class LightningDomainTests
    {
        [Test]
        public void CandidateSelection_IsDeterministicAndInputOrderIndependent()
        {
            LightningStrikeDirector first = CreateDirector();
            LightningStrikeDirector second = CreateDirector();
            first.Advance(20d);
            second.Advance(20d);
            LightningStrikeCandidate a = Candidate("candidate.a", new Vector3(0f, 10f, 0f), 0.7f);
            LightningStrikeCandidate b = Candidate("candidate.b", new Vector3(30f, 20f, 0f), 0.9f);
            var forward = new[] { a, b };
            var reverse = new[] { b, a };

            Assert.That(first.TryCreateGameplayStrike(forward, null, Vector3.zero, 0.8f, out GameplayLightningResult left), Is.True);
            Assert.That(second.TryCreateGameplayStrike(reverse, null, Vector3.zero, 0.8f, out GameplayLightningResult right), Is.True);

            Assert.That(right.StrikeEvent.CandidateId, Is.EqualTo(left.StrikeEvent.CandidateId));
            Assert.That(right.StrikeEvent.WorldPosition, Is.EqualTo(left.StrikeEvent.WorldPosition));
            Assert.That(right.Thunder.DelaySeconds, Is.EqualTo(left.Thunder.DelaySeconds));
        }

        [Test]
        public void ProtectionAndPlayerProxy_ReduceCandidateWeight()
        {
            LightningStrikeDirector director = CreateDirector();
            var world = new LightningStrikeCandidate(
                "candidate.world", new Vector3(0f, 20f, 0f), 1f, 20f, 0f, false, LightningCandidateKind.World);
            var player = new LightningStrikeCandidate(
                "candidate.player", new Vector3(0f, 20f, 0f), 1f, 20f, 0f, false, LightningCandidateKind.PlayerProxy);
            var volume = new LightningProtectionVolume(
                "protection.test", Vector3.zero, new Vector3(10f, 30f, 10f), 0.1f);

            double worldWeight = director.EvaluateCandidateWeight(world);
            double playerWeight = director.EvaluateCandidateWeight(player);
            double protectedWeight = director.EvaluateCandidateWeight(world, new[] { volume });

            Assert.That(playerWeight, Is.LessThan(worldWeight));
            Assert.That(protectedWeight, Is.LessThan(worldWeight));
        }

        [Test]
        public void GameplayStrike_UsesCooldownAndRestoreGrace()
        {
            LightningStrikeDirector director = CreateDirector();
            var candidates = new[] { Candidate("candidate.a", new Vector3(0f, 20f, 0f), 1f) };

            Assert.That(director.TryCreateGameplayStrike(candidates, null, Vector3.zero, 1f, out _), Is.False);
            director.Advance(20d);
            Assert.That(director.TryCreateGameplayStrike(candidates, null, Vector3.zero, 1f, out _), Is.True);
            Assert.That(director.TryCreateGameplayStrike(candidates, null, Vector3.zero, 1f, out _), Is.False);

            LightningDirectorSnapshot snapshot = director.CaptureSnapshot();
            var restored = CreateDirector();
            restored.Restore(snapshot);
            Assert.That(restored.TryCreateGameplayStrike(candidates, null, Vector3.zero, 1f, out _), Is.False);
            restored.Advance(100d);
            Assert.That(restored.TryCreateGameplayStrike(candidates, null, Vector3.zero, 1f, out _), Is.True);
        }

        [Test]
        public void FairnessBoost_FavoursCandidateThatWasNotRecentlySelected()
        {
            LightningStrikeDirector director = CreateDirector();
            LightningStrikeCandidate selected = Candidate("candidate.selected", new Vector3(0f, 20f, 0f), 1f);
            LightningStrikeCandidate waiting = Candidate("candidate.waiting", new Vector3(10f, 20f, 0f), 1f);
            director.Advance(20d);
            Assert.That(director.TryCreateGameplayStrike(
                new[] { selected }, null, Vector3.zero, 1f, out _), Is.True);
            director.Advance(100d);

            double selectedWeight = director.EvaluateCandidateWeight(selected);
            double waitingWeight = director.EvaluateCandidateWeight(waiting);

            Assert.That(waitingWeight, Is.GreaterThan(selectedWeight));
        }

        [Test]
        public void ThunderDelay_IsDistanceDividedBySpeedOfSound()
        {
            LightningStrikeDirector director = CreateDirector();

            double delay = director.CalculateThunderDelaySeconds(new Vector3(343f, 0f, 0f), Vector3.zero);

            Assert.That(delay, Is.EqualTo(1d).Within(0.0001d));
        }

        [Test]
        public void AmbientLightning_HasNoGameplayEffectContract()
        {
            LightningStrikeDirector director = CreateDirector();

            AmbientLightningResult result = director.CreateAmbientLightning(new Vector3(3f, 0f, 2f), 0.6f);

            Assert.That(result.StrikeEvent.Kind, Is.EqualTo(LightningEventKind.AmbientVisual));
            Assert.That(result.StrikeEvent.NonLethal, Is.True);
            Assert.That(result.Presentation.TargetWorldPosition, Is.EqualTo(new Vector3(3f, 0f, 2f)));
        }

        [Test]
        public void AmbientLightningSeries_DoesNotChangeGameplayCandidateFairnessWeight()
        {
            LightningStrikeDirector director = CreateDirector();
            LightningStrikeCandidate candidate = Candidate(
                "candidate.gameplay", new Vector3(0f, 20f, 0f), 1f);
            double weightBeforeAmbientSeries = director.EvaluateCandidateWeight(candidate);

            for (int index = 0; index < 32; index++)
            {
                director.CreateAmbientLightning(new Vector3(index, 30f, -index), 0.8f);
            }

            double weightAfterAmbientSeries = director.EvaluateCandidateWeight(candidate);

            Assert.That(director.Sequence, Is.EqualTo(32U));
            Assert.That(weightAfterAmbientSeries, Is.EqualTo(weightBeforeAmbientSeries));
        }

        [Test]
        public void Advance_WhenSimulationTimeWouldOverflow_IsRejectedWithoutMutation()
        {
            LightningStrikeDirector director = CreateDirector();
            director.Advance(double.MaxValue);
            LightningDirectorSnapshot before = director.CaptureSnapshot();

            Assert.That(
                () => director.Advance(double.MaxValue),
                Throws.TypeOf<OverflowException>());

            LightningDirectorSnapshot after = director.CaptureSnapshot();
            Assert.That(after.SimulationSeconds, Is.EqualTo(before.SimulationSeconds));
            Assert.That(after.Sequence, Is.EqualTo(before.Sequence));
            Assert.That(after.RandomState, Is.EqualTo(before.RandomState));
        }

        private static LightningStrikeDirector CreateDirector() => new LightningStrikeDirector(
            LightningStrikeConfig.CreateRemakeDesignTarget(),
            new WeatherSeed(321UL, 11UL));

        private static LightningStrikeCandidate Candidate(string id, Vector3 position, float exposure) =>
            new LightningStrikeCandidate(id, position, exposure, position.y, 0.2f, false);
    }
}
