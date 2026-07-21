using System;
using System.Collections.Generic;

namespace MSC.UI.Runtime.Capabilities
{
    public enum UiCapabilityId
    {
        SaveStorage = 0,
        MusicImport = 1,
        Mods = 2,
        DeveloperTools = 3,
        GraphicsAdvancedQuality = 4,
        GraphicsUpscaler = 5,
        GraphicsFrameGeneration = 6,
        GraphicsRayTracing = 7,
        GraphicsVramUsageMeasurement = 8,
        GraphicsPostProcessAdapter = 9,
        AudioEndpointEnumeration = 10,
        AudioGuaranteedTestEvent = 11,
        AudioIndependentVoiceBus = 12,
        AudioIndependentRadioBus = 13,
        AudioIndependentWeatherBus = 14,
        AudioIndependentThunderBus = 15,
        AudioReverbProfile = 16,
        InputInventoryAction = 17,
        InputMapAction = 18,
        InputJournalAction = 19,
        InputHandbrakeAction = 20,
        GamepadVibrationAdapter = 21,
        Autosave = 22,
        ProfileManagement = 23,
        DifficultyPresets = 24,
        PlayerMoneyProvider = 25,
        PlayerNeedsProvider = 26,
    }

    public enum UiCapabilityAvailability
    {
        Unavailable = 0,
        Supported = 1,
        DevelopmentOnly = 2,
        AdapterPending = 3,
        ReferencePending = 4,
    }

    public readonly struct UiCapabilityState : IEquatable<UiCapabilityState>
    {
        public UiCapabilityState(
            UiCapabilityId id,
            UiCapabilityAvailability availability,
            string reasonLocalizationKey)
        {
            if (string.IsNullOrWhiteSpace(reasonLocalizationKey))
            {
                throw new ArgumentException("A capability reason localization key is required.", nameof(reasonLocalizationKey));
            }

            Id = id;
            Availability = availability;
            ReasonLocalizationKey = reasonLocalizationKey;
        }

        public UiCapabilityId Id { get; }

        public UiCapabilityAvailability Availability { get; }

        public string ReasonLocalizationKey { get; }

        public bool IsInteractive => Availability == UiCapabilityAvailability.Supported ||
                                     Availability == UiCapabilityAvailability.DevelopmentOnly;

        public bool Equals(UiCapabilityState other)
        {
            return Id == other.Id &&
                   Availability == other.Availability &&
                   string.Equals(ReasonLocalizationKey, other.ReasonLocalizationKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is UiCapabilityState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Id * 397) ^ ((int)Availability * 31) ^
                       StringComparer.Ordinal.GetHashCode(ReasonLocalizationKey);
            }
        }
    }

    public interface IUiCapabilityProvider
    {
        UiCapabilityState Get(UiCapabilityId id);
    }

    public sealed class UiCapabilitySet : IUiCapabilityProvider
    {
        private const string UnspecifiedReason = "ui.capability.reason.unspecified";
        private readonly Dictionary<UiCapabilityId, UiCapabilityState> states =
            new Dictionary<UiCapabilityId, UiCapabilityState>();

        public void Set(UiCapabilityState state)
        {
            states[state.Id] = state;
        }

        public UiCapabilityState Get(UiCapabilityId id)
        {
            if (states.TryGetValue(id, out UiCapabilityState state))
            {
                return state;
            }

            return new UiCapabilityState(id, UiCapabilityAvailability.Unavailable, UnspecifiedReason);
        }

        public static UiCapabilitySet CreateBounded08ADefaults(bool isDevelopmentBuild)
        {
            UiCapabilitySet result = new UiCapabilitySet();
            foreach (UiCapabilityId id in Enum.GetValues(typeof(UiCapabilityId)))
            {
                result.Set(new UiCapabilityState(
                    id,
                    UiCapabilityAvailability.Unavailable,
                    "ui.capability.reason.not_available"));
            }

            result.Set(new UiCapabilityState(
                UiCapabilityId.DeveloperTools,
                isDevelopmentBuild ? UiCapabilityAvailability.DevelopmentOnly : UiCapabilityAvailability.Unavailable,
                isDevelopmentBuild
                    ? "ui.capability.reason.development_only"
                    : "ui.capability.reason.not_available"));
            result.Set(new UiCapabilityState(
                UiCapabilityId.Mods,
                UiCapabilityAvailability.ReferencePending,
                "ui.capability.reason.reference_pending"));
            result.Set(new UiCapabilityState(
                UiCapabilityId.GraphicsAdvancedQuality,
                UiCapabilityAvailability.AdapterPending,
                "ui.capability.reason.adapter_pending"));
            result.Set(new UiCapabilityState(
                UiCapabilityId.GraphicsPostProcessAdapter,
                UiCapabilityAvailability.AdapterPending,
                "ui.capability.reason.adapter_pending"));
            result.Set(new UiCapabilityState(
                UiCapabilityId.GamepadVibrationAdapter,
                UiCapabilityAvailability.AdapterPending,
                "ui.capability.reason.adapter_pending"));
            return result;
        }
    }
}
