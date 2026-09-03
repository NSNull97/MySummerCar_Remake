using System;
using UnityEngine;

namespace MSC.Needs
{
    public readonly struct PlayerNeedsSnapshot : IEquatable<PlayerNeedsSnapshot>
    {
        public PlayerNeedsSnapshot(
            ulong revision,
            float thirst,
            float hunger,
            float stress,
            float urine,
            float fatigue,
            float dirtiness,
            float weightKilograms,
            float intoxication,
            float hangover,
            float pendingHungerEffect,
            float pendingThirstEffect,
            float pendingWeightEffect,
            float pendingIntoxicationEffect,
            float pendingUrineEffect,
            float pendingFatigueEffect,
            float pendingDirtinessEffect)
        {
            Revision = revision;
            Thirst = ClampNeed(thirst);
            Hunger = ClampNeed(hunger);
            Stress = ClampNeed(stress);
            Urine = ClampNeed(urine);
            Fatigue = ClampNeed(fatigue);
            Dirtiness = ClampNeed(dirtiness);
            WeightKilograms = Mathf.Max(0f, weightKilograms);
            Intoxication = ClampNeed(intoxication);
            Hangover = ClampNeed(hangover);
            PendingHungerEffect = ClampPending(pendingHungerEffect);
            PendingThirstEffect = ClampPending(pendingThirstEffect);
            PendingWeightEffect = ClampPending(pendingWeightEffect);
            PendingIntoxicationEffect =
                ClampPending(pendingIntoxicationEffect);
            PendingUrineEffect = ClampPending(pendingUrineEffect);
            PendingFatigueEffect = ClampPending(pendingFatigueEffect);
            PendingDirtinessEffect = ClampPending(pendingDirtinessEffect);
        }

        public ulong Revision { get; }
        public float Thirst { get; }
        public float Hunger { get; }
        public float Stress { get; }
        public float Urine { get; }
        public float Fatigue { get; }
        public float Dirtiness { get; }
        public float WeightKilograms { get; }
        public float Intoxication { get; }
        public float Hangover { get; }
        public float PendingHungerEffect { get; }
        public float PendingThirstEffect { get; }
        public float PendingWeightEffect { get; }
        public float PendingIntoxicationEffect { get; }
        public float PendingUrineEffect { get; }
        public float PendingFatigueEffect { get; }
        public float PendingDirtinessEffect { get; }

        public float NormalizedThirst => Thirst * 0.01f;
        public float NormalizedHunger => Hunger * 0.01f;
        public float NormalizedStress => Stress * 0.01f;
        public float NormalizedUrine => Urine * 0.01f;
        public float NormalizedFatigue => Fatigue * 0.01f;
        public float NormalizedDirtiness => Dirtiness * 0.01f;

        public float GetNormalized(int index) =>
            index switch
            {
                0 => NormalizedThirst,
                1 => NormalizedHunger,
                2 => NormalizedStress,
                3 => NormalizedUrine,
                4 => NormalizedFatigue,
                5 => NormalizedDirtiness,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        public bool Equals(PlayerNeedsSnapshot other) =>
            Revision == other.Revision &&
            Thirst.Equals(other.Thirst) &&
            Hunger.Equals(other.Hunger) &&
            Stress.Equals(other.Stress) &&
            Urine.Equals(other.Urine) &&
            Fatigue.Equals(other.Fatigue) &&
            Dirtiness.Equals(other.Dirtiness) &&
            WeightKilograms.Equals(other.WeightKilograms) &&
            Intoxication.Equals(other.Intoxication) &&
            Hangover.Equals(other.Hangover) &&
            PendingHungerEffect.Equals(other.PendingHungerEffect) &&
            PendingThirstEffect.Equals(other.PendingThirstEffect) &&
            PendingWeightEffect.Equals(other.PendingWeightEffect) &&
            PendingIntoxicationEffect.Equals(
                other.PendingIntoxicationEffect) &&
            PendingUrineEffect.Equals(other.PendingUrineEffect) &&
            PendingFatigueEffect.Equals(other.PendingFatigueEffect) &&
            PendingDirtinessEffect.Equals(other.PendingDirtinessEffect);

        public override bool Equals(object obj) =>
            obj is PlayerNeedsSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Revision.GetHashCode();
                hash = hash * 397 ^ Thirst.GetHashCode();
                hash = hash * 397 ^ Hunger.GetHashCode();
                hash = hash * 397 ^ Stress.GetHashCode();
                hash = hash * 397 ^ Urine.GetHashCode();
                hash = hash * 397 ^ Fatigue.GetHashCode();
                hash = hash * 397 ^ Dirtiness.GetHashCode();
                hash = hash * 397 ^ WeightKilograms.GetHashCode();
                hash = hash * 397 ^ Intoxication.GetHashCode();
                hash = hash * 397 ^ Hangover.GetHashCode();
                hash = hash * 397 ^ PendingHungerEffect.GetHashCode();
                hash = hash * 397 ^ PendingThirstEffect.GetHashCode();
                hash = hash * 397 ^ PendingWeightEffect.GetHashCode();
                hash = hash * 397 ^ PendingIntoxicationEffect.GetHashCode();
                hash = hash * 397 ^ PendingUrineEffect.GetHashCode();
                hash = hash * 397 ^ PendingFatigueEffect.GetHashCode();
                hash = hash * 397 ^ PendingDirtinessEffect.GetHashCode();
                return hash;
            }
        }

        private static float ClampNeed(float value) =>
            float.IsFinite(value) ? Mathf.Clamp(value, 0f, 100f) : 0f;

        private static float ClampPending(float value) =>
            float.IsFinite(value) ? Mathf.Clamp(value, -1000f, 1000f) : 0f;
    }

    /// <summary>
    /// Additive change requested by an external gameplay system. Values use
    /// needs points, except WeightKilograms which uses kilograms.
    /// </summary>
    public readonly struct PlayerNeedsEffectDelta
    {
        public PlayerNeedsEffectDelta(
            float thirst = 0f,
            float hunger = 0f,
            float stress = 0f,
            float urine = 0f,
            float fatigue = 0f,
            float dirtiness = 0f,
            float weightKilograms = 0f,
            float intoxication = 0f)
        {
            Thirst = thirst;
            Hunger = hunger;
            Stress = stress;
            Urine = urine;
            Fatigue = fatigue;
            Dirtiness = dirtiness;
            WeightKilograms = weightKilograms;
            Intoxication = intoxication;
        }

        public float Thirst { get; }
        public float Hunger { get; }
        public float Stress { get; }
        public float Urine { get; }
        public float Fatigue { get; }
        public float Dirtiness { get; }
        public float WeightKilograms { get; }
        public float Intoxication { get; }

        public bool IsFinite =>
            float.IsFinite(Thirst) &&
            float.IsFinite(Hunger) &&
            float.IsFinite(Stress) &&
            float.IsFinite(Urine) &&
            float.IsFinite(Fatigue) &&
            float.IsFinite(Dirtiness) &&
            float.IsFinite(WeightKilograms) &&
            float.IsFinite(Intoxication);

        public bool HasAnyEffect =>
            Mathf.Abs(Thirst) > 0.0001f ||
            Mathf.Abs(Hunger) > 0.0001f ||
            Mathf.Abs(Stress) > 0.0001f ||
            Mathf.Abs(Urine) > 0.0001f ||
            Mathf.Abs(Fatigue) > 0.0001f ||
            Mathf.Abs(Dirtiness) > 0.0001f ||
            Mathf.Abs(WeightKilograms) > 0.0001f ||
            Mathf.Abs(Intoxication) > 0.0001f;
    }

    public interface IPlayerNeedsService
    {
        PlayerNeedsSnapshot Snapshot { get; }
        event Action<PlayerNeedsSnapshot> StateChanged;
    }

    public interface IPlayerNeedsEffectSink
    {
        bool TryApplyEffects(
            in PlayerNeedsEffectDelta immediateDelta,
            in PlayerNeedsEffectDelta delayedDelta,
            out string failure);

        bool TryApplyEffectImmediately(
            in PlayerNeedsEffectDelta delta,
            out string failure);

        bool TryQueueDelayedEffect(
            in PlayerNeedsEffectDelta delta,
            out string failure);
    }

    [Serializable]
    public sealed class PlayerNeedsSaveDto
    {
        public const int CurrentSchemaVersion = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public float thirst;
        public float hunger;
        public float stress;
        public float urine;
        public float fatigue;
        public float dirtiness;
        public float weightKilograms = 83f;
        public float intoxication;
        public float hangover;
        public float pendingHungerEffect;
        public float pendingThirstEffect;
        public float pendingWeightEffect;
        public float pendingIntoxicationEffect;
        public float pendingUrineEffect;
        public float pendingFatigueEffect;
        public float pendingDirtinessEffect;

        public static PlayerNeedsSaveDto Create(PlayerNeedsSnapshot snapshot) =>
            new PlayerNeedsSaveDto
            {
                thirst = snapshot.Thirst,
                hunger = snapshot.Hunger,
                stress = snapshot.Stress,
                urine = snapshot.Urine,
                fatigue = snapshot.Fatigue,
                dirtiness = snapshot.Dirtiness,
                weightKilograms = snapshot.WeightKilograms,
                intoxication = snapshot.Intoxication,
                hangover = snapshot.Hangover,
                pendingHungerEffect = snapshot.PendingHungerEffect,
                pendingThirstEffect = snapshot.PendingThirstEffect,
                pendingWeightEffect = snapshot.PendingWeightEffect,
                pendingIntoxicationEffect =
                    snapshot.PendingIntoxicationEffect,
                pendingUrineEffect = snapshot.PendingUrineEffect,
                pendingFatigueEffect = snapshot.PendingFatigueEffect,
                pendingDirtinessEffect = snapshot.PendingDirtinessEffect,
            };

        public static PlayerNeedsSaveDto Fresh() =>
            new PlayerNeedsSaveDto();

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure =
                    $"Unsupported player-needs schema {schemaVersion}.";
                return false;
            }

            if (!IsNeed(thirst) ||
                !IsNeed(hunger) ||
                !IsNeed(stress) ||
                !IsNeed(urine) ||
                !IsNeed(fatigue) ||
                !IsNeed(dirtiness) ||
                !IsNeed(intoxication) ||
                !IsNeed(hangover) ||
                !IsPending(pendingHungerEffect) ||
                !IsPending(pendingThirstEffect) ||
                !IsPending(pendingWeightEffect) ||
                !IsPending(pendingIntoxicationEffect) ||
                !IsPending(pendingUrineEffect) ||
                !IsPending(pendingFatigueEffect) ||
                !IsPending(pendingDirtinessEffect) ||
                !float.IsFinite(weightKilograms) ||
                weightKilograms <= 0f ||
                weightKilograms > 500f)
            {
                failure = "Player-needs values are outside supported limits.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsNeed(float value) =>
            float.IsFinite(value) && value >= 0f && value <= 100f;

        private static bool IsPending(float value) =>
            float.IsFinite(value) && value >= -1000f && value <= 1000f;
    }
}
