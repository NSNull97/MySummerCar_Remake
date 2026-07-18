using System;
using MSC.Weather.Domain;
using MSC.Weather.Wetness;
using UnityEngine;

namespace MSC.Weather.Production
{
    public enum ProductionShelterKind
    {
        Sheltered = 0,
        Interior = 1,
    }

    /// <summary>
    /// Project-owned, stable-ID shelter authoring. The current bounded contract
    /// is axis-aligned and deliberately independent from donor hierarchy names.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionShelterVolumeAuthoring : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private Vector3 localCenter;
        [SerializeField] private Vector3 localExtents = Vector3.one;
        [SerializeField] private ProductionShelterKind shelterKind =
            ProductionShelterKind.Interior;

        public string StableId => stableId;

        public ProductionShelterKind ShelterKind => shelterKind;

        public bool TryCreateVolume(
            out ShelterVolume volume,
            out string failure)
        {
            volume = default;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                failure = "Shelter volume has no project-owned stable ID.";
                return false;
            }

            if (!IsFinite(localCenter) || !IsFinite(localExtents) ||
                localExtents.x <= 0f || localExtents.y <= 0f ||
                localExtents.z <= 0f)
            {
                failure = "Shelter volume has invalid local bounds.";
                return false;
            }

            if (!Enum.IsDefined(typeof(ProductionShelterKind), shelterKind))
            {
                failure = "Shelter volume has an unsupported exposure kind.";
                return false;
            }

            if (Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f)
            {
                failure =
                    "Shelter volume rotation must remain identity while the " +
                    "bounded runtime contract uses world-axis-aligned bounds.";
                return false;
            }

            Vector3 scale = transform.lossyScale;
            Vector3 worldExtents = new Vector3(
                Math.Abs(scale.x) * localExtents.x,
                Math.Abs(scale.y) * localExtents.y,
                Math.Abs(scale.z) * localExtents.z);
            if (!IsFinite(worldExtents) || worldExtents.x <= 0f ||
                worldExtents.y <= 0f || worldExtents.z <= 0f)
            {
                failure = "Shelter volume has an invalid world scale.";
                return false;
            }

            SurfaceExposureProfile exposure =
                shelterKind == ProductionShelterKind.Interior
                    ? SurfaceExposureProfile.Interior
                    : SurfaceExposureProfile.Sheltered;
            volume = new ShelterVolume(
                stableId,
                transform.TransformPoint(localCenter),
                worldExtents,
                exposure);
            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string authoredStableId,
            Vector3 authoredLocalCenter,
            Vector3 authoredLocalExtents,
            ProductionShelterKind authoredKind)
        {
            stableId = authoredStableId;
            localCenter = authoredLocalCenter;
            localExtents = authoredLocalExtents;
            shelterKind = authoredKind;
        }
#endif

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    public static class ProductionShelterResolver
    {
        public static WeatherExposureContext Resolve(
            Vector3 worldPosition,
            ShelterVolume[] volumes)
        {
            if (!IsFinite(worldPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(worldPosition));
            }

            WeatherExposureContext result = WeatherExposureContext.Exterior;
            if (volumes == null)
            {
                return result;
            }

            for (int index = 0; index < volumes.Length; index++)
            {
                ShelterVolume candidate = volumes[index];
                if (!candidate.Contains(worldPosition))
                {
                    continue;
                }

                if (string.Equals(
                        candidate.ExposureProfile.StableId,
                        SurfaceExposureProfile.Interior.StableId,
                        StringComparison.Ordinal))
                {
                    return WeatherExposureContext.Interior;
                }

                result = WeatherExposureContext.Sheltered;
            }

            return result;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
