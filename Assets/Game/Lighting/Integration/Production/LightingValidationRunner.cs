using System;
using System.Globalization;
using System.IO;
using System.Text;
using MSC.Core.Time;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Lighting.Production
{
    [Serializable]
    public sealed class LightingValidationSnapshot
    {
        public string capturedUtc = string.Empty;
        public string caseId = string.Empty;
        public string qualityTier = string.Empty;
        public int registeredFixtures;
        public int activeLogicalLights;
        public int activeUnityLights;
        public int shadowedLights;
        public int everyFrameShadowLights;
        public int hdBeams;
        public int sdBeams;
        public float exposureEv;
        public float sunElevationDegrees;
        public string weatherId = string.Empty;
    }

    [DisallowMultipleComponent]
    public sealed class LightingValidationRunner : MonoBehaviour
    {
        [SerializeField] private ProductionLightingInstaller installer;

        public bool IsReady => installer != null && installer.IsInitialized;

        public bool TrySetEnvironment(
            double hour,
            string weatherId,
            out string failure)
        {
            if (!IsReady || hour < 0d || hour >= 24d)
            {
                failure = "Lighting validation runtime is not ready or hour is invalid.";
                return false;
            }

            var environment = installer.WorldInstaller.Environment;
            GameDate date = environment.AuthoritativeGameTime.Snapshot.Date;
            if (!environment.DevTrySetDateAndTime(
                    date,
                    hour * 3600d,
                    out failure))
            {
                return false;
            }

            environment.DevSetScheduleFrozen(true);
            if (!string.IsNullOrWhiteSpace(weatherId) &&
                !environment.DevTryApplyWeatherOverride(
                    weatherId,
                    0f,
                    out failure))
            {
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public void SetQuality(LightingQualityTier tier)
        {
            RequireReady();
            installer.RuntimeManager.SetQualityTier(tier);
        }

        public void SetCircuit(string circuitId, bool enabled)
        {
            RequireReady();
            installer.Grid.SetCircuitEnabled(circuitId, enabled);
        }

        public void SetSwitch(string switchId, bool isOn)
        {
            RequireReady();
            installer.Grid.SetSwitchState(switchId, isOn);
        }

        public void SetBusinessPresence(
            string businessId,
            bool ownerPresentAndOpen)
        {
            RequireReady();
            installer.BusinessAdapter.SetValidationOverride(
                businessId,
                ownerPresentAndOpen);
        }

        public void ClearBusinessPresenceOverrides()
        {
            RequireReady();
            installer.BusinessAdapter.ClearValidationOverrides();
        }

        public void TeleportPlayer(Vector3 position, Vector3 eulerAngles)
        {
            RequireReady();
            Transform player = installer.WorldInstaller.SpawnedPlayer.transform;
            CharacterController controller =
                player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
            {
                controller.enabled = false;
            }

            player.SetPositionAndRotation(
                position,
                Quaternion.Euler(eulerAngles));
            if (wasEnabled)
            {
                controller.enabled = true;
            }
        }

        public LightingValidationSnapshot Capture(string caseId)
        {
            RequireReady();
            LightingRuntimeManager manager = installer.RuntimeManager;
            Light[] lights = FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            int active = 0;
            int shadowed = 0;
            int everyFrame = 0;
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light == null || !light.enabled)
                {
                    continue;
                }

                active++;
                if (light.shadows != LightShadows.None)
                {
                    shadowed++;
                }

                HDAdditionalLightData hd =
                    light.GetComponent<HDAdditionalLightData>();
                if (hd != null &&
                    hd.shadowUpdateMode == ShadowUpdateMode.EveryFrame)
                {
                    everyFrame++;
                }
            }

            float exposureEv = ResolveExposureEv();

            string weatherId = installer.WorldInstaller.Environment
                .CurrentOutputs.IsValid
                ? installer.WorldInstaller.Environment.CurrentOutputs
                    .Weather.Id.Value
                : string.Empty;
            return new LightingValidationSnapshot
            {
                capturedUtc = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture),
                caseId = caseId ?? string.Empty,
                qualityTier = manager.QualityTier.ToString(),
                registeredFixtures = manager.RegisteredFixtureCount,
                activeLogicalLights = manager.ActiveLogicalLightCount,
                activeUnityLights = active,
                shadowedLights = shadowed,
                everyFrameShadowLights = everyFrame,
                hdBeams = manager.ActiveHdBeamCount,
                sdBeams = manager.ActiveSdBeamCount,
                exposureEv = exposureEv,
                sunElevationDegrees = GetComponent<EnviroLightingBridge>()
                    .GetSunElevationDegrees(),
                weatherId = weatherId,
            };
        }

        private static float ResolveExposureEv()
        {
            VolumeManager volumeManager = VolumeManager.instance;
            VolumeStack stack = volumeManager != null
                ? volumeManager.stack
                : null;
            if (stack != null)
            {
                Exposure blendedExposure = stack.GetComponent<Exposure>();
                if (blendedExposure != null && blendedExposure.active)
                {
                    return blendedExposure.fixedExposure.value;
                }
            }

            // A graphics-backed VolumeStack is not guaranteed in batch-mode
            // PlayMode tests. Fall back to the highest-priority active global
            // profile so evidence capture remains diagnostic instead of
            // throwing after the production world has finished loading.
            Volume[] volumes = FindObjectsByType<Volume>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Exposure selectedExposure = null;
            float selectedPriority = float.NegativeInfinity;
            for (int index = 0; index < volumes.Length; index++)
            {
                Volume volume = volumes[index];
                if (volume == null ||
                    !volume.enabled ||
                    !volume.isGlobal ||
                    volume.weight <= 0f ||
                    volume.priority < selectedPriority ||
                    volume.sharedProfile == null ||
                    !volume.sharedProfile.TryGet(out Exposure exposure) ||
                    exposure == null ||
                    !exposure.active)
                {
                    continue;
                }

                selectedExposure = exposure;
                selectedPriority = volume.priority;
            }

            return selectedExposure != null
                ? selectedExposure.fixedExposure.value
                : 0f;
        }

        public string CaptureEvidence(string caseId, bool screenshot)
        {
            LightingValidationSnapshot snapshot = Capture(caseId);
            string root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Docs/Lighting/Validation"));
            Directory.CreateDirectory(root);
            string safeId = Sanitize(caseId);
            string jsonPath = Path.Combine(root, safeId + ".json");
            File.WriteAllText(
                jsonPath,
                JsonUtility.ToJson(snapshot, true),
                new UTF8Encoding(false));
            if (screenshot)
            {
                string captureRoot = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "../PerformanceCaptures/Lighting"));
                Directory.CreateDirectory(captureRoot);
                ScreenCapture.CaptureScreenshot(
                    Path.Combine(captureRoot, safeId + ".png"));
            }

            return jsonPath;
        }

        private void RequireReady()
        {
            if (!IsReady)
            {
                throw new InvalidOperationException(
                    "Lighting validation runtime is not ready.");
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "lighting-validation";
            }

            var output = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                output.Append(char.IsLetterOrDigit(character) ||
                    character == '-' || character == '_'
                    ? character
                    : '-');
            }

            return output.ToString();
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            ProductionLightingInstaller configuredInstaller)
        {
            installer = configuredInstaller;
        }
#endif
    }
}
