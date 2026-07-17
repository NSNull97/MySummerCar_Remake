using System;
using UnityEngine;

namespace MSC.Weather.Wetness
{
    /// <summary>Project-owned axis-aligned exposure volume. It has no scene-name dependency.</summary>
    public readonly struct ShelterVolume
    {
        public ShelterVolume(
            string stableId,
            Vector3 center,
            Vector3 extents,
            SurfaceExposureProfile exposureProfile)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Shelter volume ID is required.", nameof(stableId));
            }

            ValidateVector(center, nameof(center));
            ValidateVector(extents, nameof(extents));
            if (extents.x <= 0f || extents.y <= 0f || extents.z <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(extents));
            }

            if (string.IsNullOrWhiteSpace(exposureProfile.StableId))
            {
                throw new ArgumentException("Shelter volume requires an exposure profile.", nameof(exposureProfile));
            }

            StableId = stableId;
            Center = center;
            Extents = extents;
            ExposureProfile = exposureProfile;
        }

        public string StableId { get; }
        public Vector3 Center { get; }
        public Vector3 Extents { get; }
        public SurfaceExposureProfile ExposureProfile { get; }

        public bool Contains(Vector3 worldPosition)
        {
            ValidateVector(worldPosition, nameof(worldPosition));
            Vector3 offset = worldPosition - Center;
            return Math.Abs(offset.x) <= Extents.x &&
                   Math.Abs(offset.y) <= Extents.y &&
                   Math.Abs(offset.z) <= Extents.z;
        }

        private static void ValidateVector(Vector3 value, string name)
        {
            if (!WetnessConfig.IsFinite(value.x) ||
                !WetnessConfig.IsFinite(value.y) ||
                !WetnessConfig.IsFinite(value.z))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
