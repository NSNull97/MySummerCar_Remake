using System;

namespace MSC.Audio.PlayerIntegration
{
    public enum PlayerVoiceReactionKind
    {
        ManualSwear = 0,
        MiddleFinger = 1,
        InsufficientFunds = 2,
        AutomaticHighStress = 3,
    }

    public readonly struct PlayerVoiceReactionDecision
    {
        public PlayerVoiceReactionDecision(
            PlayerVoiceReactionKind kind,
            int variantIndex,
            float stressRelief)
        {
            Kind = kind;
            VariantIndex = variantIndex;
            StressRelief = stressRelief;
        }

        public PlayerVoiceReactionKind Kind { get; }
        public int VariantIndex { get; }
        public float StressRelief { get; }
    }

    /// <summary>
    /// Pure presentation policy derived from the frozen donor Speech and
    /// Simulation FSM evidence. It owns no gameplay state and deliberately
    /// uses an unscaled one-second speech lock.
    /// </summary>
    public sealed class PlayerVoiceReactionPolicy
    {
        public const float AutomaticStressThreshold = 100f;
        public const float SwearStressRelief = 0.5f;
        public const double SpeechLockSeconds = 1d;

        private const uint DefaultRandomSeed = 0x7F4A7C15u;
        private const float StressRearmEpsilon = 0.001f;

        private uint randomState;
        private int previousSwearVariantIndex = -1;
        private int previousMiddleFingerVariantIndex = -1;
        private double nextAllowedUnscaledTime = double.NegativeInfinity;
        private bool automaticStressReactionArmed = true;

        public PlayerVoiceReactionPolicy(uint randomSeed = DefaultRandomSeed)
        {
            randomState = randomSeed == 0u ? DefaultRandomSeed : randomSeed;
        }

        public void ObserveStress(float stress)
        {
            if (float.IsFinite(stress) &&
                stress < AutomaticStressThreshold - StressRearmEpsilon)
            {
                automaticStressReactionArmed = true;
            }
        }

        public bool TryRequest(
            PlayerVoiceReactionKind kind,
            double unscaledTime,
            float stress,
            out PlayerVoiceReactionDecision decision)
        {
            decision = default;
            if (!Enum.IsDefined(typeof(PlayerVoiceReactionKind), kind) ||
                !double.IsFinite(unscaledTime) ||
                !float.IsFinite(stress))
            {
                return false;
            }

            ObserveStress(stress);
            if (kind == PlayerVoiceReactionKind.AutomaticHighStress &&
                (stress < AutomaticStressThreshold ||
                 !automaticStressReactionArmed))
            {
                return false;
            }

            if (unscaledTime < nextAllowedUnscaledTime)
            {
                return false;
            }

            if (kind == PlayerVoiceReactionKind.AutomaticHighStress)
            {
                automaticStressReactionArmed = false;
            }

            nextAllowedUnscaledTime = unscaledTime + SpeechLockSeconds;
            int variantIndex = NextVariantIndex(kind);
            float stressRelief = kind == PlayerVoiceReactionKind.ManualSwear ||
                                 kind == PlayerVoiceReactionKind.AutomaticHighStress
                ? SwearStressRelief
                : 0f;
            decision = new PlayerVoiceReactionDecision(
                kind,
                variantIndex,
                stressRelief);
            return true;
        }

        private int NextVariantIndex(PlayerVoiceReactionKind kind)
        {
            bool middleFinger = kind == PlayerVoiceReactionKind.MiddleFinger;
            int count = middleFinger
                ? AudioProjectIds.Events.PlayerMiddleFingerVariantCount
                : AudioProjectIds.Events.PlayerSwearVariantCount;
            int previousVariantIndex = middleFinger
                ? previousMiddleFingerVariantIndex
                : previousSwearVariantIndex;
            int candidate = (int)(NextRandom() % (uint)count);
            if (candidate == previousVariantIndex && count > 1)
            {
                candidate = (candidate + 1 +
                             (int)(NextRandom() % (uint)(count - 1))) % count;
            }

            if (middleFinger)
            {
                previousMiddleFingerVariantIndex = candidate;
            }
            else
            {
                previousSwearVariantIndex = candidate;
            }

            return candidate;
        }

        private uint NextRandom()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return randomState;
        }
    }
}
