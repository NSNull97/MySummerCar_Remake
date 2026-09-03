using System;
using System.Collections.Generic;
using MSC.Audio;
using MSC.Services;
using UnityEngine;

namespace MSC.Lighting.Production
{
    public sealed class ServiceLightingBusinessAdapter :
        IBusinessPresenceProvider
    {
        private readonly ServiceRuntime runtime;
        private readonly Dictionary<string, bool> validationOverrides =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> lastAvailableTimes =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly float shutdownDelaySeconds;

        public ServiceLightingBusinessAdapter(
            ServiceRuntime serviceRuntime,
            float configuredShutdownDelaySeconds)
        {
            runtime = serviceRuntime;
            shutdownDelaySeconds = Mathf.Max(
                0f,
                configuredShutdownDelaySeconds);
        }

        public bool IsBusinessOpenAndOwnerPresent(string businessId)
        {
            if (validationOverrides.TryGetValue(
                    businessId,
                    out bool overridden))
            {
                return overridden;
            }

            if (runtime == null || !runtime.IsInitialized)
            {
                return false;
            }

            string locationId = businessId switch
            {
                "service.teimo.shop" => "service.location.teimo-store",
                "service.teimo.pub" => "service.location.teimo-pub",
                "service.fleetari.workshop" =>
                    "service.location.workshop.fleetari",
                _ => string.Empty,
            };
            if (string.IsNullOrEmpty(locationId))
            {
                return false;
            }

            bool available = runtime.IsLocationOpen(locationId);
            if (available)
            {
                lastAvailableTimes[businessId] = Time.unscaledTime;
                return true;
            }

            return lastAvailableTimes.TryGetValue(
                       businessId,
                       out float lastAvailable) &&
                   Time.unscaledTime - lastAvailable <= shutdownDelaySeconds;
        }

        public void SetValidationOverride(string businessId, bool available)
        {
            if (string.IsNullOrWhiteSpace(businessId))
            {
                throw new ArgumentException(
                    "Business ID is required.",
                    nameof(businessId));
            }

            validationOverrides[businessId] = available;
        }

        public void ClearValidationOverrides() =>
            validationOverrides.Clear();
    }

    /// <summary>
    /// Routes switch feedback through the existing IAudioBackend. The active
    /// router selects official Wwise when installed and Unity Audio otherwise.
    /// </summary>
    public sealed class WwiseLightingAudioAdapter : ILightingAudioSink
    {
        private static readonly AudioEventId SwitchEvent =
            new AudioEventId("audio.event.lighting.switch");
        private readonly IAudioBackend backend;

        public WwiseLightingAudioAdapter(IAudioBackend configuredBackend)
        {
            backend = configuredBackend;
        }

        public void PlaySwitchEvent(
            string switchId,
            Vector3 worldPosition,
            bool isOn)
        {
            if (backend == null || !backend.IsReady)
            {
                return;
            }

            backend.PostEvent(new AudioEventRequest(
                SwitchEvent,
                worldPosition: worldPosition,
                volume01: isOn ? 1f : 0.9f));
        }
    }
}
