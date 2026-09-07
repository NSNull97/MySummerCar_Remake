using System;
using UnityEngine;

namespace MSC.Lighting
{
    [CreateAssetMenu(
        menuName = "MSC/Lighting/Quality Profile",
        fileName = "LightingQualityProfile")]
    public sealed class LightingQualityProfile : ScriptableObject
    {
        [SerializeField] private string profileId = "lighting.quality.default";
        [SerializeField, Min(0)] private int maximumShadowedLights = 12;
        [SerializeField, Min(0)] private int maximumEveryFrameShadowLights = 4;
        [SerializeField, Min(0)] private int maximumHdBeams = 3;
        [SerializeField, Min(0)] private int maximumSdBeams = 18;
        [SerializeField, Min(0f)] private float localLightDistanceMeters = 120f;
        [SerializeField, Min(0f)] private float shadowDistanceMeters = 45f;
        [SerializeField, Min(0f)] private float volumetricDistanceMeters = 100f;
        [SerializeField, Range(0f, 1f)] private float fogQuality = 0.75f;
        [SerializeField, Min(0.05f)] private float budgetRefreshSeconds = 0.2f;

        public string ProfileId => profileId;
        public int MaximumShadowedLights => maximumShadowedLights;
        public int MaximumEveryFrameShadowLights => maximumEveryFrameShadowLights;
        public int MaximumHdBeams => maximumHdBeams;
        public int MaximumSdBeams => maximumSdBeams;
        public float LocalLightDistanceMeters => localLightDistanceMeters;
        public float ShadowDistanceMeters => shadowDistanceMeters;
        public float VolumetricDistanceMeters => volumetricDistanceMeters;
        public float FogQuality => fogQuality;
        public float BudgetRefreshSeconds => budgetRefreshSeconds;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            int shadowed,
            int everyFrame,
            int hdBeams,
            int sdBeams,
            float lightDistance,
            float shadowDistance,
            float volumetricDistance,
            float configuredFogQuality,
            float refreshSeconds)
        {
            profileId = id;
            maximumShadowedLights = Mathf.Max(0, shadowed);
            maximumEveryFrameShadowLights = Mathf.Max(0, everyFrame);
            maximumHdBeams = Mathf.Max(0, hdBeams);
            maximumSdBeams = Mathf.Max(0, sdBeams);
            localLightDistanceMeters = Mathf.Max(0f, lightDistance);
            shadowDistanceMeters = Mathf.Max(0f, shadowDistance);
            volumetricDistanceMeters = Mathf.Max(0f, volumetricDistance);
            fogQuality = Mathf.Clamp01(configuredFogQuality);
            budgetRefreshSeconds = Mathf.Max(0.05f, refreshSeconds);
        }
#endif
    }

}
