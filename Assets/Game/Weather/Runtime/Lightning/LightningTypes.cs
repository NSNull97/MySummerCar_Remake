using System;
using MSC.Weather.Domain;
using UnityEngine;

namespace MSC.Weather.Lightning
{
    public enum LightningEventKind
    {
        AmbientVisual = 0,
        GameplayStrike = 1,
    }

    public enum LightningCandidateKind
    {
        World = 0,
        Attractor = 1,
        PlayerProxy = 2,
    }

    public readonly struct LightningStrikeConfig
    {
        public const string RemakeDesignTargetConfigId = "weather.lightning.remake_design_target.v1";
        public const string RemakeDesignTargetProvenance = "RemakeDesignTarget";

        public LightningStrikeConfig(
            string configId,
            double globalCooldownSeconds,
            double candidateCooldownSeconds,
            double restoreGraceSeconds,
            float heightReferenceMeters,
            float heightWeightMultiplier,
            float attractorWeightMultiplier,
            float protectionWeightMultiplier,
            float playerProxyWeightMultiplier,
            float fairnessBoostPerMiss,
            float maximumFairnessBoost,
            float speedOfSoundMetersPerSecond,
            bool nonLethalMode,
            string provenance)
        {
            if (string.IsNullOrWhiteSpace(configId))
            {
                throw new ArgumentException("Lightning config ID is required.", nameof(configId));
            }

            ValidateNonNegative(globalCooldownSeconds, nameof(globalCooldownSeconds));
            ValidateNonNegative(candidateCooldownSeconds, nameof(candidateCooldownSeconds));
            ValidateNonNegative(restoreGraceSeconds, nameof(restoreGraceSeconds));
            ValidatePositive(heightReferenceMeters, nameof(heightReferenceMeters));
            ValidateNonNegative(heightWeightMultiplier, nameof(heightWeightMultiplier));
            ValidateNonNegative(attractorWeightMultiplier, nameof(attractorWeightMultiplier));
            Validate01(protectionWeightMultiplier, nameof(protectionWeightMultiplier));
            Validate01(playerProxyWeightMultiplier, nameof(playerProxyWeightMultiplier));
            ValidateNonNegative(fairnessBoostPerMiss, nameof(fairnessBoostPerMiss));
            ValidateNonNegative(maximumFairnessBoost, nameof(maximumFairnessBoost));
            ValidatePositive(speedOfSoundMetersPerSecond, nameof(speedOfSoundMetersPerSecond));
            if (string.IsNullOrWhiteSpace(provenance))
            {
                throw new ArgumentException("Lightning config provenance is required.", nameof(provenance));
            }

            ConfigId = configId;
            GlobalCooldownSeconds = globalCooldownSeconds;
            CandidateCooldownSeconds = candidateCooldownSeconds;
            RestoreGraceSeconds = restoreGraceSeconds;
            HeightReferenceMeters = heightReferenceMeters;
            HeightWeightMultiplier = heightWeightMultiplier;
            AttractorWeightMultiplier = attractorWeightMultiplier;
            ProtectionWeightMultiplier = protectionWeightMultiplier;
            PlayerProxyWeightMultiplier = playerProxyWeightMultiplier;
            FairnessBoostPerMiss = fairnessBoostPerMiss;
            MaximumFairnessBoost = maximumFairnessBoost;
            SpeedOfSoundMetersPerSecond = speedOfSoundMetersPerSecond;
            NonLethalMode = nonLethalMode;
            Provenance = provenance;
        }

        public string ConfigId { get; }
        public double GlobalCooldownSeconds { get; }
        public double CandidateCooldownSeconds { get; }
        public double RestoreGraceSeconds { get; }
        public float HeightReferenceMeters { get; }
        public float HeightWeightMultiplier { get; }
        public float AttractorWeightMultiplier { get; }
        public float ProtectionWeightMultiplier { get; }
        public float PlayerProxyWeightMultiplier { get; }
        public float FairnessBoostPerMiss { get; }
        public float MaximumFairnessBoost { get; }
        public float SpeedOfSoundMetersPerSecond { get; }
        public bool NonLethalMode { get; }
        public string Provenance { get; }

        public static LightningStrikeConfig CreateRemakeDesignTarget() => new LightningStrikeConfig(
            RemakeDesignTargetConfigId,
            globalCooldownSeconds: 22d,
            candidateCooldownSeconds: 75d,
            restoreGraceSeconds: 20d,
            heightReferenceMeters: 30f,
            heightWeightMultiplier: 1.25f,
            attractorWeightMultiplier: 3f,
            protectionWeightMultiplier: 0.04f,
            playerProxyWeightMultiplier: 0.08f,
            fairnessBoostPerMiss: 0.08f,
            maximumFairnessBoost: 1.5f,
            speedOfSoundMetersPerSecond: 343f,
            nonLethalMode: true,
            provenance: RemakeDesignTargetProvenance);

        internal static void Validate01(float value, string name)
        {
            if (!IsFinite(value) || value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        internal static void ValidateNonNegative(float value, string name)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        internal static void ValidateNonNegative(double value, string name)
        {
            if (!IsFinite(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        internal static void ValidatePositive(float value, string name)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        internal static void ValidateVector(Vector3 value, string name)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }

    public readonly struct LightningStrikeCandidate
    {
        public LightningStrikeCandidate(
            string stableId,
            Vector3 worldPosition,
            float exposure01,
            float heightMeters,
            float attractorStrength01,
            bool isProtected,
            LightningCandidateKind kind = LightningCandidateKind.World)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Lightning candidate requires a project-owned stable ID.", nameof(stableId));
            }

            LightningStrikeConfig.ValidateVector(worldPosition, nameof(worldPosition));
            LightningStrikeConfig.Validate01(exposure01, nameof(exposure01));
            LightningStrikeConfig.ValidateNonNegative(heightMeters, nameof(heightMeters));
            LightningStrikeConfig.Validate01(attractorStrength01, nameof(attractorStrength01));
            if (!Enum.IsDefined(typeof(LightningCandidateKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            StableId = stableId;
            WorldPosition = worldPosition;
            Exposure01 = exposure01;
            HeightMeters = heightMeters;
            AttractorStrength01 = attractorStrength01;
            IsProtected = isProtected;
            Kind = kind;
        }

        public string StableId { get; }
        public Vector3 WorldPosition { get; }
        public float Exposure01 { get; }
        public float HeightMeters { get; }
        public float AttractorStrength01 { get; }
        public bool IsProtected { get; }
        public LightningCandidateKind Kind { get; }
    }

    public readonly struct LightningAttractor
    {
        public LightningAttractor(string stableId, Vector3 worldPosition, float heightMeters, float strength01)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Lightning attractor requires a stable ID.", nameof(stableId));
            }

            LightningStrikeConfig.ValidateVector(worldPosition, nameof(worldPosition));
            LightningStrikeConfig.ValidateNonNegative(heightMeters, nameof(heightMeters));
            LightningStrikeConfig.Validate01(strength01, nameof(strength01));
            StableId = stableId;
            WorldPosition = worldPosition;
            HeightMeters = heightMeters;
            Strength01 = strength01;
        }

        public string StableId { get; }
        public Vector3 WorldPosition { get; }
        public float HeightMeters { get; }
        public float Strength01 { get; }

        public LightningStrikeCandidate ToCandidate(float exposure01 = 1f) =>
            new LightningStrikeCandidate(
                StableId,
                WorldPosition,
                exposure01,
                HeightMeters,
                Strength01,
                isProtected: false,
                LightningCandidateKind.Attractor);
    }

    public readonly struct LightningProtectionVolume
    {
        public LightningProtectionVolume(string stableId, Vector3 center, Vector3 extents, float weightMultiplier01)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Lightning protection volume requires a stable ID.", nameof(stableId));
            }

            LightningStrikeConfig.ValidateVector(center, nameof(center));
            LightningStrikeConfig.ValidateVector(extents, nameof(extents));
            if (extents.x <= 0f || extents.y <= 0f || extents.z <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(extents));
            }

            LightningStrikeConfig.Validate01(weightMultiplier01, nameof(weightMultiplier01));
            StableId = stableId;
            Center = center;
            Extents = extents;
            WeightMultiplier01 = weightMultiplier01;
        }

        public string StableId { get; }
        public Vector3 Center { get; }
        public Vector3 Extents { get; }
        public float WeightMultiplier01 { get; }

        public bool Contains(Vector3 worldPosition)
        {
            LightningStrikeConfig.ValidateVector(worldPosition, nameof(worldPosition));
            Vector3 offset = worldPosition - Center;
            return Math.Abs(offset.x) <= Extents.x &&
                   Math.Abs(offset.y) <= Extents.y &&
                   Math.Abs(offset.z) <= Extents.z;
        }
    }

    public readonly struct LightningStrikeEvent
    {
        public LightningStrikeEvent(
            uint sequence,
            LightningEventKind kind,
            string candidateId,
            Vector3 worldPosition,
            float intensity01,
            bool nonLethal)
        {
            if (sequence == 0U)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            if (string.IsNullOrWhiteSpace(candidateId))
            {
                throw new ArgumentException("Lightning event requires a stable target ID.", nameof(candidateId));
            }

            LightningStrikeConfig.ValidateVector(worldPosition, nameof(worldPosition));
            LightningStrikeConfig.Validate01(intensity01, nameof(intensity01));
            Sequence = sequence;
            Kind = kind;
            CandidateId = candidateId;
            WorldPosition = worldPosition;
            Intensity01 = intensity01;
            NonLethal = nonLethal;
        }

        public uint Sequence { get; }
        public LightningEventKind Kind { get; }
        public string CandidateId { get; }
        public Vector3 WorldPosition { get; }
        public float Intensity01 { get; }
        public bool NonLethal { get; }
    }

    public readonly struct LightningPresentationRequest
    {
        public LightningPresentationRequest(uint sequence, LightningEventKind kind, Vector3 targetWorldPosition, float intensity01)
        {
            if (sequence == 0U)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            LightningStrikeConfig.ValidateVector(targetWorldPosition, nameof(targetWorldPosition));
            LightningStrikeConfig.Validate01(intensity01, nameof(intensity01));
            Sequence = sequence;
            Kind = kind;
            TargetWorldPosition = targetWorldPosition;
            Intensity01 = intensity01;
        }

        public uint Sequence { get; }
        public LightningEventKind Kind { get; }
        public Vector3 TargetWorldPosition { get; }
        public float Intensity01 { get; }
    }

    public readonly struct ThunderAudioRequest
    {
        public ThunderAudioRequest(uint sequence, Vector3 worldPosition, float intensity01, double delaySeconds)
        {
            if (sequence == 0U)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            LightningStrikeConfig.ValidateVector(worldPosition, nameof(worldPosition));
            LightningStrikeConfig.Validate01(intensity01, nameof(intensity01));
            LightningStrikeConfig.ValidateNonNegative(delaySeconds, nameof(delaySeconds));
            Sequence = sequence;
            WorldPosition = worldPosition;
            Intensity01 = intensity01;
            DelaySeconds = delaySeconds;
        }

        public uint Sequence { get; }
        public Vector3 WorldPosition { get; }
        public float Intensity01 { get; }
        public double DelaySeconds { get; }
    }

    public readonly struct LightningGameplayEffectRequest
    {
        public LightningGameplayEffectRequest(
            uint sequence,
            string candidateId,
            Vector3 worldPosition,
            float intensity01,
            bool nonLethal)
        {
            if (sequence == 0U || string.IsNullOrWhiteSpace(candidateId))
            {
                throw new ArgumentException("Gameplay lightning effect requires sequence and target identity.");
            }

            LightningStrikeConfig.ValidateVector(worldPosition, nameof(worldPosition));
            LightningStrikeConfig.Validate01(intensity01, nameof(intensity01));
            Sequence = sequence;
            CandidateId = candidateId;
            WorldPosition = worldPosition;
            Intensity01 = intensity01;
            NonLethal = nonLethal;
        }

        public uint Sequence { get; }
        public string CandidateId { get; }
        public Vector3 WorldPosition { get; }
        public float Intensity01 { get; }
        public bool NonLethal { get; }
    }

    public readonly struct AmbientLightningResult
    {
        public AmbientLightningResult(LightningStrikeEvent strikeEvent, LightningPresentationRequest presentation)
        {
            StrikeEvent = strikeEvent;
            Presentation = presentation;
        }

        public LightningStrikeEvent StrikeEvent { get; }
        public LightningPresentationRequest Presentation { get; }
    }

    public readonly struct GameplayLightningResult
    {
        public GameplayLightningResult(
            LightningStrikeEvent strikeEvent,
            LightningPresentationRequest presentation,
            ThunderAudioRequest thunder,
            LightningGameplayEffectRequest gameplayEffect)
        {
            StrikeEvent = strikeEvent;
            Presentation = presentation;
            Thunder = thunder;
            GameplayEffect = gameplayEffect;
        }

        public LightningStrikeEvent StrikeEvent { get; }
        public LightningPresentationRequest Presentation { get; }
        public ThunderAudioRequest Thunder { get; }
        public LightningGameplayEffectRequest GameplayEffect { get; }
    }

    public readonly struct LightningCandidateFairnessState
    {
        public LightningCandidateFairnessState(
            string stableId,
            double cooldownUntilSeconds,
            uint lastStrikeSequence,
            uint strikeCount)
        {
            StableId = stableId;
            CooldownUntilSeconds = cooldownUntilSeconds;
            LastStrikeSequence = lastStrikeSequence;
            StrikeCount = strikeCount;
        }

        public string StableId { get; }
        public double CooldownUntilSeconds { get; }
        public uint LastStrikeSequence { get; }
        public uint StrikeCount { get; }
    }

    public readonly struct LightningDirectorSnapshot
    {
        public LightningDirectorSnapshot(
            string configId,
            double simulationSeconds,
            double globalCooldownUntilSeconds,
            double restoreGraceUntilSeconds,
            uint sequence,
            bool nonLethalMode,
            WeatherRandomState randomState,
            LightningCandidateFairnessState[] candidateStates)
        {
            ConfigId = configId;
            SimulationSeconds = simulationSeconds;
            GlobalCooldownUntilSeconds = globalCooldownUntilSeconds;
            RestoreGraceUntilSeconds = restoreGraceUntilSeconds;
            Sequence = sequence;
            NonLethalMode = nonLethalMode;
            RandomState = randomState;
            CandidateStates = candidateStates ?? Array.Empty<LightningCandidateFairnessState>();
        }

        public string ConfigId { get; }
        public double SimulationSeconds { get; }
        public double GlobalCooldownUntilSeconds { get; }
        public double RestoreGraceUntilSeconds { get; }
        public uint Sequence { get; }
        public bool NonLethalMode { get; }
        public WeatherRandomState RandomState { get; }
        public LightningCandidateFairnessState[] CandidateStates { get; }
    }
}
