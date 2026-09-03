using System;
using UnityEngine;

namespace MSC.Lighting
{
    [CreateAssetMenu(
        menuName = "MSC/Lighting/Profile Catalog",
        fileName = "LightingProfileCatalog")]
    public sealed class LightingProfileCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId =
            "lighting.catalog.phase1.local-lighting";
        [SerializeField] private LightFixtureProfile[] profiles =
            Array.Empty<LightFixtureProfile>();
        [SerializeField] private LightingQualityProfile[] qualityProfiles =
            Array.Empty<LightingQualityProfile>();
        [SerializeField] private LightingCalibrationProfile calibration;

        public string CatalogId => catalogId;
        public LightFixtureProfile[] Profiles => profiles;
        public LightingQualityProfile[] QualityProfiles => qualityProfiles;
        public LightingCalibrationProfile Calibration => calibration;

        public LightFixtureProfile GetRequiredProfile(
            LightFixtureCategory category)
        {
            for (int index = 0; index < profiles.Length; index++)
            {
                LightFixtureProfile profile = profiles[index];
                if (profile != null && profile.Category == category)
                {
                    return profile;
                }
            }

            throw new InvalidOperationException(
                $"Lighting profile catalog '{catalogId}' has no profile " +
                $"for category '{category}'.");
        }

        public LightingQualityProfile GetRequiredQuality(
            LightingQualityTier tier)
        {
            string suffix = tier.ToString().ToLowerInvariant();
            for (int index = 0; index < qualityProfiles.Length; index++)
            {
                LightingQualityProfile quality = qualityProfiles[index];
                if (quality != null && quality.ProfileId.EndsWith(
                        suffix,
                        StringComparison.Ordinal))
                {
                    return quality;
                }
            }

            throw new InvalidOperationException(
                $"Lighting profile catalog '{catalogId}' has no quality " +
                $"profile for tier '{tier}'.");
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            LightFixtureProfile[] configuredProfiles,
            LightingQualityProfile[] configuredQualityProfiles,
            LightingCalibrationProfile configuredCalibration)
        {
            catalogId = id ?? string.Empty;
            profiles = configuredProfiles ?? Array.Empty<LightFixtureProfile>();
            qualityProfiles = configuredQualityProfiles ??
                Array.Empty<LightingQualityProfile>();
            calibration = configuredCalibration;
        }
#endif
    }
}
