using System;
using UnityEngine;

namespace MSC.Home
{
    public enum HomeActionKind
    {
        KitchenTapOpen = 0,
        KitchenTapConsume = 1,
        KitchenTapClose = 2,
        KitchenTapToggle = 3,
        ShowerSwitchToggle = 10,
        ShowerValveToggle = 11,
        ElectricSaunaPowerToggle = 20,
        ElectricSaunaTimerCycle = 21,
        ElectricSaunaSteam = 22,
        ToiletUse = 30,
        SinkWash = 31,
        FridgePowerToggle = 40,
        StovePowerToggle = 41,
        TelevisionPowerToggle = 42,
        FireplaceFireToggle = 43,
        MangalFireToggle = 44,
    }

    /// <summary>
    /// Project-owned action identity. Scene hierarchy and donor object names are
    /// deliberately not part of the domestic runtime contract.
    /// </summary>
    public static class HomeActionIds
    {
        public const string KitchenTapOpen = "home.kitchen.tap.open";
        public const string KitchenTapConsume = "home.kitchen.tap.consume";
        public const string KitchenTapClose = "home.kitchen.tap.close";
        public const string KitchenTapToggle = "home.kitchen.tap.toggle";
        public const string ShowerSwitchToggle =
            "home.shower.switch.toggle";
        public const string ShowerValveToggle =
            "home.shower.valve.toggle";
        public const string ElectricSaunaPowerToggle =
            "home.sauna.electric.power.toggle";
        public const string ElectricSaunaTimerCycle =
            "home.sauna.electric.timer.cycle";
        public const string ElectricSaunaSteam =
            "home.sauna.electric.steam";
        public const string ToiletUse = "home.toilet.use";
        public const string SinkWash = "home.sink.wash";
        public const string FridgePowerToggle =
            "home.fridge.power.toggle";
        public const string StovePowerToggle =
            "home.stove.power.toggle";
        public const string TelevisionPowerToggle =
            "home.television.power.toggle";
        public const string FireplaceFireToggle =
            "home.fireplace.fire.toggle";
        public const string MangalFireToggle =
            "home.cottage.mangal.fire.toggle";

        public static string For(HomeActionKind action) =>
            action switch
            {
                HomeActionKind.KitchenTapOpen => KitchenTapOpen,
                HomeActionKind.KitchenTapConsume => KitchenTapConsume,
                HomeActionKind.KitchenTapClose => KitchenTapClose,
                HomeActionKind.KitchenTapToggle => KitchenTapToggle,
                HomeActionKind.ShowerSwitchToggle => ShowerSwitchToggle,
                HomeActionKind.ShowerValveToggle => ShowerValveToggle,
                HomeActionKind.ElectricSaunaPowerToggle =>
                    ElectricSaunaPowerToggle,
                HomeActionKind.ElectricSaunaTimerCycle =>
                    ElectricSaunaTimerCycle,
                HomeActionKind.ElectricSaunaSteam =>
                    ElectricSaunaSteam,
                HomeActionKind.ToiletUse => ToiletUse,
                HomeActionKind.SinkWash => SinkWash,
                HomeActionKind.FridgePowerToggle => FridgePowerToggle,
                HomeActionKind.StovePowerToggle => StovePowerToggle,
                HomeActionKind.TelevisionPowerToggle =>
                    TelevisionPowerToggle,
                HomeActionKind.FireplaceFireToggle =>
                    FireplaceFireToggle,
                HomeActionKind.MangalFireToggle => MangalFireToggle,
                _ => string.Empty,
            };

        public static bool Matches(
            string stableActionId,
            HomeActionKind action) =>
            string.Equals(
                stableActionId,
                For(action),
                StringComparison.Ordinal);
    }

    /// <summary>
    /// Donor-evidenced electric-sauna control ranges. Angles are authoritative
    /// values; scene transforms are presentation only.
    /// </summary>
    public static class HomeSaunaControlRange
    {
        public const float MinimumTimerDegrees = 1f;
        public const float MaximumTimerDegrees = 120f;
        public const float TimerStepDegrees = 10f;
        public const float TimerSecondsPerDegree = 6f;
        public const float MinimumHeatDegrees = 1f;
        public const float MaximumHeatDegrees = 150f;
        public const float HeatStepDegrees = 15f;
        public const float HeatDivisor = 300f;

        public static float ClampTimer(float value) =>
            Mathf.Clamp(
                value,
                MinimumTimerDegrees,
                MaximumTimerDegrees);

        public static float ClampHeat(float value) =>
            Mathf.Clamp(
                value,
                MinimumHeatDegrees,
                MaximumHeatDegrees);
    }

    /// <summary>
    /// Temporary Phase 1 values. Every coefficient remains provisional until
    /// repeated measurements against the locked donor version are recorded.
    /// </summary>
    public readonly struct HomeProvisionalTuning
    {
        public HomeProvisionalTuning(
            float kitchenTapThirstRelief,
            float kitchenTapUrineIncrease,
            float sinkDirtinessRelief,
            float showerCleaningRadiusMeters,
            float showerDirtinessReliefPerSecond,
            float showerNeedWriteIntervalSeconds,
            float saunaAmbientTemperatureCelsius,
            float saunaMaximumTemperatureCelsius,
            float saunaSteamMinimumTemperatureCelsius,
            float saunaHeatingDegreesPerSecond,
            float saunaCoolingDegreesPerSecond,
            float saunaSteamHeatLossDegrees,
            float saunaSteamImpulse,
            float saunaSteamDecayPerSecond,
            float saunaTimerStepSeconds,
            float saunaTimerMaximumSeconds)
        {
            KitchenTapThirstRelief = kitchenTapThirstRelief;
            KitchenTapUrineIncrease = kitchenTapUrineIncrease;
            SinkDirtinessRelief = sinkDirtinessRelief;
            ShowerCleaningRadiusMeters = showerCleaningRadiusMeters;
            ShowerDirtinessReliefPerSecond =
                showerDirtinessReliefPerSecond;
            ShowerNeedWriteIntervalSeconds =
                showerNeedWriteIntervalSeconds;
            SaunaAmbientTemperatureCelsius =
                saunaAmbientTemperatureCelsius;
            SaunaMaximumTemperatureCelsius =
                saunaMaximumTemperatureCelsius;
            SaunaSteamMinimumTemperatureCelsius =
                saunaSteamMinimumTemperatureCelsius;
            SaunaHeatingDegreesPerSecond =
                saunaHeatingDegreesPerSecond;
            SaunaCoolingDegreesPerSecond =
                saunaCoolingDegreesPerSecond;
            SaunaSteamHeatLossDegrees = saunaSteamHeatLossDegrees;
            SaunaSteamImpulse = saunaSteamImpulse;
            SaunaSteamDecayPerSecond = saunaSteamDecayPerSecond;
            SaunaTimerStepSeconds = saunaTimerStepSeconds;
            SaunaTimerMaximumSeconds = saunaTimerMaximumSeconds;
        }

        public float KitchenTapThirstRelief { get; }
        public float KitchenTapUrineIncrease { get; }
        public float SinkDirtinessRelief { get; }
        public float ShowerCleaningRadiusMeters { get; }
        public float ShowerDirtinessReliefPerSecond { get; }
        public float ShowerNeedWriteIntervalSeconds { get; }
        public float SaunaAmbientTemperatureCelsius { get; }
        public float SaunaMaximumTemperatureCelsius { get; }
        public float SaunaSteamMinimumTemperatureCelsius { get; }
        public float SaunaHeatingDegreesPerSecond { get; }
        public float SaunaCoolingDegreesPerSecond { get; }
        public float SaunaSteamHeatLossDegrees { get; }
        public float SaunaSteamImpulse { get; }
        public float SaunaSteamDecayPerSecond { get; }
        public float SaunaTimerStepSeconds { get; }
        public float SaunaTimerMaximumSeconds { get; }

        public static HomeProvisionalTuning Defaults =>
            new HomeProvisionalTuning(
                kitchenTapThirstRelief: 4f,
                kitchenTapUrineIncrease: 1.5f,
                sinkDirtinessRelief: 5f,
                showerCleaningRadiusMeters: 1.6f,
                showerDirtinessReliefPerSecond: 2f,
                showerNeedWriteIntervalSeconds: 0.25f,
                saunaAmbientTemperatureCelsius: 20f,
                saunaMaximumTemperatureCelsius: 110f,
                saunaSteamMinimumTemperatureCelsius: 60f,
                saunaHeatingDegreesPerSecond: 0.075f,
                saunaCoolingDegreesPerSecond: 0.025f,
                saunaSteamHeatLossDegrees: 2f,
                saunaSteamImpulse: 0.35f,
                saunaSteamDecayPerSecond: 0.1f,
                saunaTimerStepSeconds:
                    HomeSaunaControlRange.TimerStepDegrees *
                    HomeSaunaControlRange.TimerSecondsPerDegree,
                saunaTimerMaximumSeconds:
                    HomeSaunaControlRange.MaximumTimerDegrees *
                    HomeSaunaControlRange.TimerSecondsPerDegree);

        public bool TryValidate(out string failure)
        {
            if (!IsNonNegative(KitchenTapThirstRelief) ||
                !IsNonNegative(KitchenTapUrineIncrease) ||
                !IsNonNegative(SinkDirtinessRelief) ||
                !IsPositive(ShowerCleaningRadiusMeters) ||
                !IsNonNegative(ShowerDirtinessReliefPerSecond) ||
                !IsPositive(ShowerNeedWriteIntervalSeconds) ||
                !float.IsFinite(SaunaAmbientTemperatureCelsius) ||
                !float.IsFinite(SaunaMaximumTemperatureCelsius) ||
                !float.IsFinite(SaunaSteamMinimumTemperatureCelsius) ||
                !IsNonNegative(SaunaHeatingDegreesPerSecond) ||
                !IsNonNegative(SaunaCoolingDegreesPerSecond) ||
                !IsNonNegative(SaunaSteamHeatLossDegrees) ||
                !IsNonNegative(SaunaSteamImpulse) ||
                SaunaSteamImpulse > 1f ||
                !IsNonNegative(SaunaSteamDecayPerSecond) ||
                !IsPositive(SaunaTimerStepSeconds) ||
                !IsPositive(SaunaTimerMaximumSeconds) ||
                SaunaMaximumTemperatureCelsius <=
                    SaunaAmbientTemperatureCelsius ||
                SaunaSteamMinimumTemperatureCelsius <
                    SaunaAmbientTemperatureCelsius ||
                SaunaSteamMinimumTemperatureCelsius >
                    SaunaMaximumTemperatureCelsius ||
                SaunaTimerStepSeconds > SaunaTimerMaximumSeconds)
            {
                failure =
                    "Provisional home tuning contains invalid values.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsPositive(float value) =>
            float.IsFinite(value) && value > 0f;

        private static bool IsNonNegative(float value) =>
            float.IsFinite(value) && value >= 0f;
    }

    public readonly struct HomeStateSnapshot :
        IEquatable<HomeStateSnapshot>
    {
        public HomeStateSnapshot(
            ulong revision,
            bool kitchenTapOpen,
            bool showerSwitchOn,
            bool showerValveOpen,
            bool electricSaunaPowerOn,
            float electricSaunaHeatKnobDegrees,
            float electricSaunaTimerKnobDegrees,
            float electricSaunaTimerSecondsRemaining,
            float saunaTemperatureCelsius,
            float saunaSteamNormalized,
            bool fridgePowered,
            bool fridgeDoorOpen,
            bool stovePowered,
            bool televisionPowered,
            bool fireplaceLit,
            bool mangalLit)
        {
            Revision = revision;
            KitchenTapOpen = kitchenTapOpen;
            ShowerSwitchOn = showerSwitchOn;
            ShowerValveOpen = showerValveOpen;
            ElectricSaunaPowerOn = electricSaunaPowerOn;
            ElectricSaunaHeatKnobDegrees =
                electricSaunaHeatKnobDegrees;
            ElectricSaunaTimerKnobDegrees =
                electricSaunaTimerKnobDegrees;
            ElectricSaunaTimerSecondsRemaining =
                electricSaunaTimerSecondsRemaining;
            SaunaTemperatureCelsius = saunaTemperatureCelsius;
            SaunaSteamNormalized = saunaSteamNormalized;
            FridgePowered = fridgePowered;
            FridgeDoorOpen = fridgeDoorOpen;
            StovePowered = stovePowered;
            TelevisionPowered = televisionPowered;
            FireplaceLit = fireplaceLit;
            MangalLit = mangalLit;
        }

        public ulong Revision { get; }
        public bool KitchenTapOpen { get; }
        public bool ShowerSwitchOn { get; }
        public bool ShowerValveOpen { get; }
        public bool ShowerHeadSelected => ShowerSwitchOn;
        public bool ShowerHeadFlowing =>
            ShowerValveOpen && ShowerHeadSelected;
        public bool ShowerTapFlowing =>
            ShowerValveOpen && !ShowerHeadSelected;
        public bool ShowerFlowing => ShowerHeadFlowing;
        public bool ElectricSaunaPowerOn { get; }
        public float ElectricSaunaHeatKnobDegrees { get; }
        public float ElectricSaunaTimerKnobDegrees { get; }
        public float ElectricSaunaMaximumHeatNormalized =>
            ElectricSaunaHeatKnobDegrees /
            HomeSaunaControlRange.HeatDivisor;
        public float ElectricSaunaTimerSecondsRemaining { get; }
        public float SaunaTemperatureCelsius { get; }
        public float SaunaSteamNormalized { get; }
        public bool FridgePowered { get; }
        public bool FridgeDoorOpen { get; }
        public bool StovePowered { get; }
        public bool TelevisionPowered { get; }
        public bool FireplaceLit { get; }
        public bool MangalLit { get; }

        public bool Equals(HomeStateSnapshot other) =>
            Revision == other.Revision &&
            KitchenTapOpen == other.KitchenTapOpen &&
            ShowerSwitchOn == other.ShowerSwitchOn &&
            ShowerValveOpen == other.ShowerValveOpen &&
            ElectricSaunaPowerOn == other.ElectricSaunaPowerOn &&
            ElectricSaunaHeatKnobDegrees.Equals(
                other.ElectricSaunaHeatKnobDegrees) &&
            ElectricSaunaTimerKnobDegrees.Equals(
                other.ElectricSaunaTimerKnobDegrees) &&
            ElectricSaunaTimerSecondsRemaining.Equals(
                other.ElectricSaunaTimerSecondsRemaining) &&
            SaunaTemperatureCelsius.Equals(
                other.SaunaTemperatureCelsius) &&
            SaunaSteamNormalized.Equals(other.SaunaSteamNormalized) &&
            FridgePowered == other.FridgePowered &&
            FridgeDoorOpen == other.FridgeDoorOpen &&
            StovePowered == other.StovePowered &&
            TelevisionPowered == other.TelevisionPowered &&
            FireplaceLit == other.FireplaceLit &&
            MangalLit == other.MangalLit;

        public override bool Equals(object obj) =>
            obj is HomeStateSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Revision.GetHashCode();
                hash = hash * 397 ^ KitchenTapOpen.GetHashCode();
                hash = hash * 397 ^ ShowerSwitchOn.GetHashCode();
                hash = hash * 397 ^ ShowerValveOpen.GetHashCode();
                hash = hash * 397 ^
                    ElectricSaunaPowerOn.GetHashCode();
                hash = hash * 397 ^
                    ElectricSaunaHeatKnobDegrees.GetHashCode();
                hash = hash * 397 ^
                    ElectricSaunaTimerKnobDegrees.GetHashCode();
                hash = hash * 397 ^
                    ElectricSaunaTimerSecondsRemaining.GetHashCode();
                hash = hash * 397 ^
                    SaunaTemperatureCelsius.GetHashCode();
                hash = hash * 397 ^
                    SaunaSteamNormalized.GetHashCode();
                hash = hash * 397 ^ FridgePowered.GetHashCode();
                hash = hash * 397 ^ FridgeDoorOpen.GetHashCode();
                hash = hash * 397 ^ StovePowered.GetHashCode();
                hash = hash * 397 ^ TelevisionPowered.GetHashCode();
                hash = hash * 397 ^ FireplaceLit.GetHashCode();
                hash = hash * 397 ^ MangalLit.GetHashCode();
                return hash;
            }
        }
    }

    [Serializable]
    public sealed class HomeStateDto
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public bool kitchenTapOpen;
        public bool showerSwitchOn;
        public bool showerValveOpen;
        public bool electricSaunaPowerOn;
        public float electricSaunaHeatKnobDegrees =
            HomeSaunaControlRange.MinimumHeatDegrees;
        public float electricSaunaTimerKnobDegrees =
            HomeSaunaControlRange.MinimumTimerDegrees;
        public float electricSaunaTimerSecondsRemaining;
        public float saunaTemperatureCelsius = 20f;
        public float saunaSteamNormalized;
        public bool fridgePowered;
        public bool fridgeDoorOpen;
        public bool stovePowered;
        public bool televisionPowered;
        public bool fireplaceLit;
        public bool mangalLit;

        public static HomeStateDto Create(HomeStateSnapshot snapshot) =>
            new HomeStateDto
            {
                kitchenTapOpen = snapshot.KitchenTapOpen,
                showerSwitchOn = snapshot.ShowerSwitchOn,
                showerValveOpen = snapshot.ShowerValveOpen,
                electricSaunaPowerOn =
                    snapshot.ElectricSaunaPowerOn,
                electricSaunaHeatKnobDegrees =
                    snapshot.ElectricSaunaHeatKnobDegrees,
                electricSaunaTimerKnobDegrees =
                    snapshot.ElectricSaunaTimerKnobDegrees,
                electricSaunaTimerSecondsRemaining =
                    snapshot.ElectricSaunaTimerSecondsRemaining,
                saunaTemperatureCelsius =
                    snapshot.SaunaTemperatureCelsius,
                saunaSteamNormalized = snapshot.SaunaSteamNormalized,
                fridgePowered = snapshot.FridgePowered,
                fridgeDoorOpen = snapshot.FridgeDoorOpen,
                stovePowered = snapshot.StovePowered,
                televisionPowered = snapshot.TelevisionPowered,
                fireplaceLit = snapshot.FireplaceLit,
                mangalLit = snapshot.MangalLit,
            };

        public static HomeStateDto Fresh(
            float ambientTemperatureCelsius = 20f) =>
            new HomeStateDto
            {
                // The private remake starts with the shower head selected so
                // opening the water valve immediately produces a shower.
                showerSwitchOn = true,
                fridgePowered = true,
                saunaTemperatureCelsius =
                    ambientTemperatureCelsius,
            };

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure = $"Unsupported home-state schema {schemaVersion}.";
                return false;
            }

            if (!float.IsFinite(electricSaunaHeatKnobDegrees) ||
                electricSaunaHeatKnobDegrees <
                    HomeSaunaControlRange.MinimumHeatDegrees ||
                electricSaunaHeatKnobDegrees >
                    HomeSaunaControlRange.MaximumHeatDegrees ||
                !float.IsFinite(electricSaunaTimerKnobDegrees) ||
                electricSaunaTimerKnobDegrees <
                    HomeSaunaControlRange.MinimumTimerDegrees ||
                electricSaunaTimerKnobDegrees >
                    HomeSaunaControlRange.MaximumTimerDegrees ||
                !float.IsFinite(electricSaunaTimerSecondsRemaining) ||
                electricSaunaTimerSecondsRemaining < 0f ||
                electricSaunaTimerSecondsRemaining >
                    HomeSaunaControlRange.MaximumTimerDegrees *
                    HomeSaunaControlRange.TimerSecondsPerDegree ||
                !float.IsFinite(saunaTemperatureCelsius) ||
                saunaTemperatureCelsius < -80f ||
                saunaTemperatureCelsius > 250f ||
                !float.IsFinite(saunaSteamNormalized) ||
                saunaSteamNormalized < 0f ||
                saunaSteamNormalized > 1f)
            {
                failure = "Home-state values are outside supported limits.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    public readonly struct HomeActionCompleted
    {
        public HomeActionCompleted(
            string stableActionId,
            HomeActionKind action,
            bool succeeded,
            string message,
            HomeStateSnapshot state)
        {
            StableActionId = stableActionId ?? string.Empty;
            Action = action;
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            State = state;
        }

        public string StableActionId { get; }
        public HomeActionKind Action { get; }
        public bool Succeeded { get; }
        public string Message { get; }
        public HomeStateSnapshot State { get; }
    }
}
