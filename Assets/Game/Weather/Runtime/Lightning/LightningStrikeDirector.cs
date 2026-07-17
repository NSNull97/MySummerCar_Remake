using System;
using System.Collections.Generic;
using MSC.Weather.Domain;
using UnityEngine;

namespace MSC.Weather.Lightning
{
    public sealed class LightningStrikeDirector
    {
        private sealed class CandidateRuntimeState
        {
            public double CooldownUntilSeconds;
            public uint LastStrikeSequence;
            public uint StrikeCount;
        }

        private readonly struct WeightedCandidate
        {
            public WeightedCandidate(LightningStrikeCandidate candidate, double weight)
            {
                Candidate = candidate;
                Weight = weight;
            }

            public LightningStrikeCandidate Candidate { get; }
            public double Weight { get; }
        }

        private readonly LightningStrikeConfig config;
        private readonly WeatherRandom random;
        private readonly Dictionary<string, CandidateRuntimeState> candidateStates =
            new Dictionary<string, CandidateRuntimeState>(StringComparer.Ordinal);

        private double simulationSeconds;
        private double globalCooldownUntilSeconds;
        private double restoreGraceUntilSeconds;
        private uint sequence;
        private bool nonLethalMode;

        public LightningStrikeDirector(LightningStrikeConfig config, WeatherSeed seed)
        {
            this.config = config;
            if (string.IsNullOrWhiteSpace(config.ConfigId))
            {
                throw new ArgumentException("A valid lightning config is required.", nameof(config));
            }

            random = new WeatherRandom(seed);
            nonLethalMode = config.NonLethalMode;
            restoreGraceUntilSeconds = config.RestoreGraceSeconds;
        }

        public string ConfigId => config.ConfigId;
        public double SimulationSeconds => simulationSeconds;
        public bool NonLethalMode => nonLethalMode;
        public bool IsGameplayStrikeAllowed =>
            simulationSeconds >= globalCooldownUntilSeconds && simulationSeconds >= restoreGraceUntilSeconds;
        public uint Sequence => sequence;

        public void Advance(double deltaSeconds)
        {
            LightningStrikeConfig.ValidateNonNegative(deltaSeconds, nameof(deltaSeconds));
            double nextSimulationSeconds = simulationSeconds + deltaSeconds;
            if (!double.IsFinite(nextSimulationSeconds))
            {
                throw new OverflowException("Lightning simulation time exceeded its finite range.");
            }

            simulationSeconds = nextSimulationSeconds;
        }

        public void SetNonLethalMode(bool enabled) => nonLethalMode = enabled;

        public AmbientLightningResult CreateAmbientLightning(Vector3 targetWorldPosition, float intensity01)
        {
            LightningStrikeConfig.ValidateVector(targetWorldPosition, nameof(targetWorldPosition));
            LightningStrikeConfig.Validate01(intensity01, nameof(intensity01));
            uint requestSequence = NextSequence();
            var strikeEvent = new LightningStrikeEvent(
                requestSequence,
                LightningEventKind.AmbientVisual,
                "lightning.ambient",
                targetWorldPosition,
                intensity01,
                nonLethal: true);
            var presentation = new LightningPresentationRequest(
                requestSequence,
                LightningEventKind.AmbientVisual,
                targetWorldPosition,
                intensity01);
            return new AmbientLightningResult(strikeEvent, presentation);
        }

        public bool TryCreateGameplayStrike(
            IReadOnlyList<LightningStrikeCandidate> candidates,
            IReadOnlyList<LightningProtectionVolume> protectionVolumes,
            Vector3 listenerWorldPosition,
            float intensity01,
            out GameplayLightningResult result)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            LightningStrikeConfig.ValidateVector(listenerWorldPosition, nameof(listenerWorldPosition));
            LightningStrikeConfig.Validate01(intensity01, nameof(intensity01));
            if (!IsGameplayStrikeAllowed)
            {
                result = default;
                return false;
            }

            var weighted = new List<WeightedCandidate>(candidates.Count);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < candidates.Count; index++)
            {
                LightningStrikeCandidate candidate = candidates[index];
                if (!seenIds.Add(candidate.StableId))
                {
                    throw new ArgumentException($"Duplicate lightning candidate ID '{candidate.StableId}'.", nameof(candidates));
                }

                double weight = EvaluateCandidateWeight(candidate, protectionVolumes);
                if (weight > 0d)
                {
                    weighted.Add(new WeightedCandidate(candidate, weight));
                }
            }

            if (weighted.Count == 0)
            {
                result = default;
                return false;
            }

            weighted.Sort((left, right) =>
                string.Compare(left.Candidate.StableId, right.Candidate.StableId, StringComparison.Ordinal));
            double totalWeight = 0d;
            for (int index = 0; index < weighted.Count; index++)
            {
                totalWeight += weighted[index].Weight;
            }

            double draw = random.NextDouble01() * totalWeight;
            int selectedIndex = weighted.Count - 1;
            double accumulated = 0d;
            for (int index = 0; index < weighted.Count; index++)
            {
                accumulated += weighted[index].Weight;
                if (draw < accumulated)
                {
                    selectedIndex = index;
                    break;
                }
            }

            LightningStrikeCandidate selected = weighted[selectedIndex].Candidate;
            uint requestSequence = NextSequence();
            CandidateRuntimeState selectedState = GetOrCreateState(selected.StableId);
            selectedState.CooldownUntilSeconds = simulationSeconds + config.CandidateCooldownSeconds;
            selectedState.LastStrikeSequence = requestSequence;
            selectedState.StrikeCount++;
            globalCooldownUntilSeconds = simulationSeconds + config.GlobalCooldownSeconds;

            var strikeEvent = new LightningStrikeEvent(
                requestSequence,
                LightningEventKind.GameplayStrike,
                selected.StableId,
                selected.WorldPosition,
                intensity01,
                nonLethalMode);
            var presentation = new LightningPresentationRequest(
                requestSequence,
                LightningEventKind.GameplayStrike,
                selected.WorldPosition,
                intensity01);
            double thunderDelay = CalculateThunderDelaySeconds(selected.WorldPosition, listenerWorldPosition);
            var thunder = new ThunderAudioRequest(requestSequence, selected.WorldPosition, intensity01, thunderDelay);
            var gameplayEffect = new LightningGameplayEffectRequest(
                requestSequence,
                selected.StableId,
                selected.WorldPosition,
                intensity01,
                nonLethalMode);
            result = new GameplayLightningResult(strikeEvent, presentation, thunder, gameplayEffect);
            return true;
        }

        public double EvaluateCandidateWeight(
            in LightningStrikeCandidate candidate,
            IReadOnlyList<LightningProtectionVolume> protectionVolumes = null)
        {
            if (candidate.Exposure01 <= 0f)
            {
                return 0d;
            }

            // A player proxy can inform diagnostics, but is never itself a direct strike target.
            if (candidate.Kind == LightningCandidateKind.PlayerProxy)
            {
                return 0d;
            }

            CandidateRuntimeState runtimeState = GetOrCreateState(candidate.StableId);
            if (simulationSeconds < runtimeState.CooldownUntilSeconds)
            {
                return 0d;
            }

            double normalizedHeight = Math.Min(1d, candidate.HeightMeters / config.HeightReferenceMeters);
            double heightWeight = 1d + (normalizedHeight * config.HeightWeightMultiplier);
            double attractorWeight = 1d + (candidate.AttractorStrength01 * config.AttractorWeightMultiplier);
            double protectionWeight = candidate.IsProtected ? config.ProtectionWeightMultiplier : 1d;
            if (protectionVolumes != null)
            {
                for (int index = 0; index < protectionVolumes.Count; index++)
                {
                    LightningProtectionVolume volume = protectionVolumes[index];
                    if (volume.Contains(candidate.WorldPosition))
                    {
                        protectionWeight *= volume.WeightMultiplier01;
                    }
                }
            }

            uint completedGameplayStrikes = CalculateCompletedGameplayStrikeCount();
            uint missedStrikeCount = completedGameplayStrikes >= runtimeState.StrikeCount
                ? completedGameplayStrikes - runtimeState.StrikeCount
                : 0U;
            double fairnessBonus = Math.Min(
                config.MaximumFairnessBoost,
                missedStrikeCount * config.FairnessBoostPerMiss);
            return candidate.Exposure01 * heightWeight * attractorWeight * protectionWeight *
                   (1d + fairnessBonus);
        }

        public double CalculateThunderDelaySeconds(Vector3 strikeWorldPosition, Vector3 listenerWorldPosition)
        {
            LightningStrikeConfig.ValidateVector(strikeWorldPosition, nameof(strikeWorldPosition));
            LightningStrikeConfig.ValidateVector(listenerWorldPosition, nameof(listenerWorldPosition));
            return Vector3.Distance(strikeWorldPosition, listenerWorldPosition) /
                   config.SpeedOfSoundMetersPerSecond;
        }

        public LightningDirectorSnapshot CaptureSnapshot()
        {
            var keys = new List<string>(candidateStates.Keys);
            keys.Sort(StringComparer.Ordinal);
            var states = new LightningCandidateFairnessState[keys.Count];
            for (int index = 0; index < keys.Count; index++)
            {
                string key = keys[index];
                CandidateRuntimeState value = candidateStates[key];
                states[index] = new LightningCandidateFairnessState(
                    key,
                    value.CooldownUntilSeconds,
                    value.LastStrikeSequence,
                    value.StrikeCount);
            }

            return new LightningDirectorSnapshot(
                config.ConfigId,
                simulationSeconds,
                globalCooldownUntilSeconds,
                restoreGraceUntilSeconds,
                sequence,
                nonLethalMode,
                random.CaptureState(),
                states);
        }

        public void ValidateSnapshot(in LightningDirectorSnapshot snapshot)
        {
            if (!string.Equals(snapshot.ConfigId, config.ConfigId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Lightning snapshot config ID does not match the active config.", nameof(snapshot));
            }

            LightningStrikeConfig.ValidateNonNegative(snapshot.SimulationSeconds, nameof(snapshot));
            LightningStrikeConfig.ValidateNonNegative(snapshot.GlobalCooldownUntilSeconds, nameof(snapshot));
            LightningStrikeConfig.ValidateNonNegative(snapshot.RestoreGraceUntilSeconds, nameof(snapshot));
            if (!snapshot.RandomState.IsValid)
            {
                throw new ArgumentException("Lightning snapshot RNG state is invalid.", nameof(snapshot));
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            LightningCandidateFairnessState[] states = snapshot.CandidateStates ??
                Array.Empty<LightningCandidateFairnessState>();
            for (int index = 0; index < states.Length; index++)
            {
                LightningCandidateFairnessState value = states[index];
                if (string.IsNullOrWhiteSpace(value.StableId) || !seenIds.Add(value.StableId))
                {
                    throw new ArgumentException("Lightning snapshot candidate IDs must be non-empty and unique.", nameof(snapshot));
                }

                LightningStrikeConfig.ValidateNonNegative(value.CooldownUntilSeconds, nameof(snapshot));
                if (value.LastStrikeSequence > snapshot.Sequence)
                {
                    throw new ArgumentException("Candidate strike sequence exceeds the director sequence.", nameof(snapshot));
                }
            }
        }

        public void Restore(in LightningDirectorSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);

            simulationSeconds = snapshot.SimulationSeconds;
            globalCooldownUntilSeconds = snapshot.GlobalCooldownUntilSeconds;
            restoreGraceUntilSeconds = Math.Max(
                snapshot.RestoreGraceUntilSeconds,
                simulationSeconds + config.RestoreGraceSeconds);
            sequence = snapshot.Sequence;
            nonLethalMode = snapshot.NonLethalMode;
            random.RestoreState(snapshot.RandomState);
            candidateStates.Clear();
            LightningCandidateFairnessState[] states = snapshot.CandidateStates ??
                Array.Empty<LightningCandidateFairnessState>();
            for (int index = 0; index < states.Length; index++)
            {
                LightningCandidateFairnessState value = states[index];
                candidateStates.Add(
                    value.StableId,
                    new CandidateRuntimeState
                    {
                        CooldownUntilSeconds = value.CooldownUntilSeconds,
                        LastStrikeSequence = value.LastStrikeSequence,
                        StrikeCount = value.StrikeCount,
                    });
            }
        }

        private CandidateRuntimeState GetOrCreateState(string stableId)
        {
            if (!candidateStates.TryGetValue(stableId, out CandidateRuntimeState state))
            {
                state = new CandidateRuntimeState();
                candidateStates.Add(stableId, state);
            }

            return state;
        }

        private uint CalculateCompletedGameplayStrikeCount()
        {
            ulong total = 0UL;
            foreach (CandidateRuntimeState state in candidateStates.Values)
            {
                total += state.StrikeCount;
                if (total >= uint.MaxValue)
                {
                    return uint.MaxValue;
                }
            }

            return (uint)total;
        }

        private uint NextSequence()
        {
            unchecked
            {
                sequence++;
                if (sequence == 0U)
                {
                    sequence = 1U;
                }
            }

            return sequence;
        }
    }
}
