using UnityEngine;

namespace MSC.Lighting
{
    [CreateAssetMenu(
        menuName = "MSC/Lighting/Calibration Profile",
        fileName = "LightingCalibrationProfile")]
    public sealed class LightingCalibrationProfile : ScriptableObject
    {
        [SerializeField] private string calibrationId =
            "lighting.calibration.phase1.1990s-finland";
        [SerializeField] private string exposureOwner =
            "NativeHdrpWeatherBridge";
        [SerializeField] private float dayFixedExposureEv = 12.5f;
        [SerializeField] private float nightFixedExposureEv = 7.25f;
        [SerializeField] private float duskOnSunElevationDegrees = -2.5f;
        [SerializeField] private float dawnOffSunElevationDegrees = 0.5f;
        [SerializeField, Min(0f)] private float businessShutdownDelaySeconds = 4f;

        public string CalibrationId => calibrationId;
        public string ExposureOwner => exposureOwner;
        public float DayFixedExposureEv => dayFixedExposureEv;
        public float NightFixedExposureEv => nightFixedExposureEv;
        public float DuskOnSunElevationDegrees => duskOnSunElevationDegrees;
        public float DawnOffSunElevationDegrees => dawnOffSunElevationDegrees;
        public float BusinessShutdownDelaySeconds => businessShutdownDelaySeconds;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            string configuredExposureOwner,
            float dayEv,
            float nightEv,
            float duskOn,
            float dawnOff,
            float shutdownDelay)
        {
            calibrationId = id;
            exposureOwner = configuredExposureOwner;
            dayFixedExposureEv = dayEv;
            nightFixedExposureEv = nightEv;
            duskOnSunElevationDegrees = duskOn;
            dawnOffSunElevationDegrees = dawnOff;
            businessShutdownDelaySeconds = Mathf.Max(0f, shutdownDelay);
        }
#endif
    }
}
